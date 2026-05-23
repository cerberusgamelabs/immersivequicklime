using System;
using System.Linq;
using System.Text;
using ImmersiveQuicklime.code.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ImmersiveQuicklime.code.BlockEntities;

public class BlockEntityDomedGrate : BlockEntity
{
    private static readonly Vec3d SmokeOffset = new(0.5, 0.25, 0.5);
    private LimestoneKilnConfig kilnConfig = new();
    private string fuelCode = "";
    private string fuelType = "";
    private string cachedCellPositions = "";
    private int cachedLimestoneCount;
    private int fuelQuantity;
    private double burningUntilTotalHours;
    private double requiredBurnHours;

    public bool Lit { get; private set; }
    public bool Failed { get; private set; }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        kilnConfig = (Block as BlockDomedGrate)?.KilnConfig ?? new LimestoneKilnConfig();

        if (api.Side == EnumAppSide.Server)
        {
            RegisterGameTickListener(OnServerTick, 2000);
        }
        else
        {
            RegisterGameTickListener(OnClientParticleTick, 500);
        }
    }

    public bool OnInteract(IPlayer byPlayer)
    {
        if (Api == null || byPlayer?.InventoryManager?.ActiveHotbarSlot == null || Lit)
        {
            return false;
        }

        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (slot.Empty || !TryMatchFuel(slot.Itemstack, out var config))
        {
            return false;
        }

        if (!TryResolveStructure(out var structure, out var failureCode))
        {
            TriggerError(failureCode);
            return true;
        }

        if (fuelQuantity > 0 && !slot.Itemstack.Collectible.Code.Equals(AssetLocation.Create(fuelCode)))
        {
            TriggerError("immersivequicklime:error-mixed-fuel");
            return true;
        }

        int maxFuelQuantity = CalculateMaxFuelQuantity(structure, config);
        int remainingFuelCapacity = maxFuelQuantity - fuelQuantity;
        if (remainingFuelCapacity <= 0)
        {
            TriggerError("immersivequicklime:error-fuel-full");
            return true;
        }

        int moved = Math.Min(slot.StackSize, remainingFuelCapacity);
        slot.TakeOut(moved);
        slot.MarkDirty();

        fuelCode = config.Code;
        fuelType = config.Type;
        fuelQuantity += moved;
        MarkDirty(true);
        return true;
    }

    public bool CanIgnite()
    {
        return !Lit && fuelQuantity > 0;
    }

    public void TryIgnite(IPlayer? byPlayer)
    {
        if (!CanIgnite())
        {
            return;
        }

        if (!TryResolveStructure(out var structure, out var failureCode))
        {
            TriggerError(failureCode);
            return;
        }

        requiredBurnHours = CalculateRequiredBurnHours(structure);
        if (TotalFuelHours() < requiredBurnHours)
        {
            TriggerError("immersivequicklime:error-not-enough-fuel");
            return;
        }

        Lit = true;
        Failed = false;
        CacheStructure(structure);
        burningUntilTotalHours = Api.World.Calendar.TotalHours + requiredBurnHours;
        SyncLitBlockVariant();
        MarkVisualDirty();
        MarkDirty(true);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        if (!string.IsNullOrEmpty(fuelCode))
        {
            dsc.AppendLine(Lang.Get("immersivequicklime:fuel-line", fuelQuantity, Lang.Get(fuelCode)));
        }
        else
        {
            dsc.AppendLine(Lang.Get("immersivequicklime:no-fuel"));
        }

        if (Lit)
        {
            dsc.AppendLine(Lang.Get("immersivequicklime:burning-hours", Math.Max(0, burningUntilTotalHours - Api.World.Calendar.TotalHours).ToString("0.0")));
        }
        else if (Failed)
        {
            dsc.AppendLine(Lang.Get("immersivequicklime:failed"));
        }
        else
        {
            dsc.AppendLine(Lang.Get("immersivequicklime:unlit"));
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        bool wasLit = Lit;
        base.FromTreeAttributes(tree, worldForResolving);
        fuelCode = tree.GetString(nameof(fuelCode));
        fuelType = tree.GetString(nameof(fuelType));
        cachedCellPositions = tree.GetString(nameof(cachedCellPositions));
        cachedLimestoneCount = tree.GetInt(nameof(cachedLimestoneCount));
        fuelQuantity = tree.GetInt(nameof(fuelQuantity));
        burningUntilTotalHours = tree.GetDouble(nameof(burningUntilTotalHours));
        requiredBurnHours = tree.GetDouble(nameof(requiredBurnHours));
        Lit = tree.GetBool(nameof(Lit));
        Failed = tree.GetBool(nameof(Failed));

        if (wasLit != Lit)
        {
            SyncLitBlockVariant();
            MarkVisualDirty();
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString(nameof(fuelCode), fuelCode);
        tree.SetString(nameof(fuelType), fuelType);
        tree.SetString(nameof(cachedCellPositions), cachedCellPositions);
        tree.SetInt(nameof(cachedLimestoneCount), cachedLimestoneCount);
        tree.SetInt(nameof(fuelQuantity), fuelQuantity);
        tree.SetDouble(nameof(burningUntilTotalHours), burningUntilTotalHours);
        tree.SetDouble(nameof(requiredBurnHours), requiredBurnHours);
        tree.SetBool(nameof(Lit), Lit);
        tree.SetBool(nameof(Failed), Failed);
    }

    private void OnServerTick(float dt)
    {
        if (!Lit)
        {
            return;
        }

        if (Api.World.Calendar.TotalHours < burningUntilTotalHours)
        {
            return;
        }

        try
        {
            LimestonePitStructure? structure = GetCachedStructure();
            if (structure != null)
            {
                ConvertStructure(structure);
                MaybeBreakGrate();
            }
            else
            {
                Failed = true;
            }
        }
        catch (Exception ex)
        {
            Failed = true;
            Api.World.Logger.Error("[ImmersiveQuicklime] Kiln completion error at {0}: {1}", Pos, ex);
        }

        Lit = false;
        SyncLitBlockVariant();
        ConsumeFuel();
        MarkVisualDirty();
        MarkDirty(true);
    }

    private void OnClientParticleTick(float dt)
    {
        if (!Lit || Api?.Side != EnumAppSide.Client)
        {
            return;
        }

        Vec3d minPos = Pos.ToVec3d().AddCopy(SmokeOffset.X - 0.08, SmokeOffset.Y, SmokeOffset.Z - 0.08);
        Vec3d maxPos = Pos.ToVec3d().AddCopy(SmokeOffset.X + 0.12, SmokeOffset.Y + 0.06, SmokeOffset.Z + 0.12);
        Api.World.SpawnParticles(
            3f,
            ColorUtil.ToRgba(145, 70, 70, 70),
            minPos,
            maxPos,
            new Vec3f(-0.025f, 0.04f, -0.025f),
            new Vec3f(0.025f, 0.12f, 0.025f),
            1.9f,
            0f,
            0.55f,
            EnumParticleModel.Quad,
            null
        );
    }

    private void ConvertStructure(LimestonePitStructure structure)
    {
        var quicklime = Api.World.GetItem(new AssetLocation("game", "quicklime"));
        var limestone = Api.World.GetItem(new AssetLocation("game", "stone-limestone"));
        if (quicklime == null || limestone == null)
        {
            Failed = true;
            return;
        }

        int totalInput = structure.LimestoneCount;
        int recipes = totalInput / kilnConfig.InputUnits;
        int quicklimeCount = recipes * kilnConfig.OutputUnits;
        int leftoverLimestone = totalInput % kilnConfig.InputUnits;
        ClearSourceCells(structure);
        PopulateQuicklimePiles(structure, quicklimeCount);
        SpawnRemainingOutput(limestone, leftoverLimestone);
    }

    private void MaybeBreakGrate()
    {
        if (Api.World.Rand.NextDouble() < kilnConfig.BreakChance)
        {
            Api.World.BlockAccessor.SetBlock(0, Pos);
        }
    }

    private void ConsumeFuel()
    {
        fuelCode = "";
        fuelType = "";
        cachedCellPositions = "";
        cachedLimestoneCount = 0;
        fuelQuantity = 0;
        burningUntilTotalHours = 0;
        requiredBurnHours = 0;
    }

    private bool TryResolveStructure(out LimestonePitStructure structure, out string failureCode)
    {
        return LimestonePitStructureResolver.TryResolve(Api, Pos.DownCopy(), Pos, kilnConfig.MaxDimension, kilnConfig.MaxHeight, out structure, out failureCode);
    }

    private double TotalFuelHours()
    {
        var config = kilnConfig.Fuels.FirstOrDefault(f => f.Code == fuelCode && f.Type == fuelType);
        return config == null ? 0 : config.BurnHours * fuelQuantity;
    }

    private double CalculateRequiredBurnHours(LimestonePitStructure structure)
    {
        int recipes = structure.LimestoneCount / kilnConfig.InputUnits;
        return kilnConfig.WarmupHours + recipes * kilnConfig.HoursPerPair;
    }

    private int CalculateMaxFuelQuantity(LimestonePitStructure structure, LimestoneFuelConfig fuelConfig)
    {
        return Math.Max(1, (int)Math.Ceiling(CalculateRequiredBurnHours(structure) / fuelConfig.BurnHours));
    }

    private bool TryMatchFuel(ItemStack stack, out LimestoneFuelConfig config)
    {
        string type = stack.Class == EnumItemClass.Block ? "block" : "item";
        config = kilnConfig.Fuels.FirstOrDefault(f => f.Type == type && stack.Collectible.Code.Equals(AssetLocation.Create(f.Code)));
        return config != null;
    }

    private void ClearSourceCells(LimestonePitStructure structure)
    {
        foreach (BlockPos pos in structure.Cells)
        {
            Api.World.BlockAccessor.SetBlock(0, pos);
            Api.World.BlockAccessor.MarkBlockDirty(pos);
            Api.World.BlockAccessor.MarkBlockModified(pos);
        }
    }

    private void PopulateQuicklimePiles(LimestonePitStructure structure, int totalQuicklime)
    {
        foreach (BlockPos pos in structure.Cells)
        {
            int pileCount = Math.Min(BlockQuicklimePile.MaxUnitsPerBlock, totalQuicklime);
            totalQuicklime -= pileCount;

            if (pileCount <= 0)
            {
                continue;
            }

            int layer = System.Math.Max(1, (int)System.Math.Ceiling(pileCount / (double)BlockQuicklimePile.UnitsPerLayer));
            Block pileBlock = Api.World.GetBlock(new AssetLocation("immersivequicklime", $"quicklimepile-{layer}"));
            if (pileBlock == null)
            {
                Failed = true;
                return;
            }

            Api.World.BlockAccessor.SetBlock(pileBlock.Id, pos);
            if (Api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityQuicklimePile be)
            {
                be.SetPileSize(pileCount);
            }
            else
            {
                Failed = true;
                return;
            }
        }
    }

    private void SpawnRemainingOutput(Item item, int remainingCount)
    {
        if (remainingCount <= 0)
        {
            return;
        }

        var spawnPos = Pos.ToVec3d().Add(0.5, 0.75, 0.5);
        while (remainingCount > 0)
        {
            int stackSize = Math.Min(item.MaxStackSize, remainingCount);
            Api.World.SpawnItemEntity(new ItemStack(item, stackSize), spawnPos);
            remainingCount -= stackSize;
        }
    }

    private void TriggerError(string langCode)
    {
        if (Api is ICoreClientAPI capi)
        {
            capi.TriggerIngameError(this, langCode, Lang.Get(langCode));
        }
    }

    private void MarkVisualDirty()
    {
        if (Api?.World?.BlockAccessor == null)
        {
            return;
        }

        Api.World.BlockAccessor.MarkBlockDirty(Pos);
        Api.World.BlockAccessor.MarkBlockModified(Pos);
    }

    private void SyncLitBlockVariant()
    {
        if (Api?.World?.BlockAccessor == null || Block?.Code == null)
        {
            return;
        }

        if (Api.Side != EnumAppSide.Server)
        {
            return;
        }

        string desiredPath = Lit ? "domedgrate-lit" : "domedgrate-off";
        if (Block.Code.Path == desiredPath)
        {
            return;
        }

        Block targetBlock = Api.World.GetBlock(new AssetLocation("immersivequicklime", desiredPath));
        if (targetBlock == null || targetBlock.Id == 0)
        {
            return;
        }

        Api.World.BlockAccessor.ExchangeBlock(targetBlock.Id, Pos);

        Api.World.BlockAccessor.MarkBlockDirty(Pos);
        Api.World.BlockAccessor.MarkBlockModified(Pos);
        Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
    }

    private void CacheStructure(LimestonePitStructure structure)
    {
        cachedLimestoneCount = structure.LimestoneCount;
        cachedCellPositions = string.Join(";", structure.Cells.Select(pos => $"{pos.X},{pos.Y},{pos.Z}"));
    }

    private LimestonePitStructure? GetCachedStructure()
    {
        if (cachedLimestoneCount <= 0 || string.IsNullOrWhiteSpace(cachedCellPositions))
        {
            return null;
        }

        LimestonePitStructure structure = new()
        {
            LimestoneCount = cachedLimestoneCount
        };

        foreach (string token in cachedCellPositions.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = token.Split(',');
            if (parts.Length != 3)
            {
                continue;
            }

            if (!int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y) || !int.TryParse(parts[2], out int z))
            {
                continue;
            }

            structure.Cells.Add(new BlockPos(x, y, z));
        }

        return structure.Cells.Count == 0 ? null : structure;
    }
}
