using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using ImmersiveQuicklime.code.Blocks;

namespace ImmersiveQuicklime.code.BlockEntities;

public class BlockEntityQuicklimePile : BlockEntity
{
    private static readonly AssetLocation QuicklimeCode = new("game", "quicklime");

    private int quicklimeCount;

    public int QuicklimeCount => quicklimeCount;

    public bool TryAdd(ItemSlot slot, IPlayer byPlayer)
    {
        if (Api?.Side != EnumAppSide.Server || slot?.Empty != false || !BlockQuicklimePile.IsQuicklimeItem(slot.Itemstack))
        {
            return true;
        }

        int moved = Math.Min(BlockQuicklimePile.MaxUnitsPerBlock - quicklimeCount, slot.StackSize);
        if (moved <= 0)
        {
            return true;
        }

        slot.TakeOut(moved);
        slot.MarkDirty();
        SetPileSize(quicklimeCount + moved);
        return true;
    }

    public bool TryTake(IPlayer byPlayer)
    {
        if (Api?.Side != EnumAppSide.Server || quicklimeCount <= 0)
        {
            return true;
        }

        Item item = Api.World.GetItem(QuicklimeCode);
        if (item == null)
        {
            return false;
        }

        ItemStack stack = new(item, 1);
        if (byPlayer?.InventoryManager?.TryGiveItemstack(stack, true) != true)
        {
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }

        SetPileSize(quicklimeCount - 1);
        return true;
    }

    public void SetPileSize(int count)
    {
        quicklimeCount = GameMath.Clamp(count, 0, BlockQuicklimePile.MaxUnitsPerBlock);
        UpdateBlockState();
        MarkDirty(true);
    }

    public void SpillAll()
    {
        if (Api?.World == null || quicklimeCount <= 0)
        {
            return;
        }

        Item item = Api.World.GetItem(QuicklimeCode);
        if (item == null)
        {
            return;
        }

        int remaining = quicklimeCount;
        while (remaining > 0)
        {
            int stackSize = Math.Min(item.MaxStackSize, remaining);
            Api.World.SpawnItemEntity(new ItemStack(item, stackSize), Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            remaining -= stackSize;
        }

        quicklimeCount = 0;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        quicklimeCount = tree.GetInt(nameof(quicklimeCount));
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetInt(nameof(quicklimeCount), quicklimeCount);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (quicklimeCount <= 0)
        {
            quicklimeCount = InferCountFromVariant();
        }
    }

    private void UpdateBlockState()
    {
        if (Api?.World == null || Api.Side != EnumAppSide.Server)
        {
            return;
        }

        if (quicklimeCount <= 0)
        {
            Api.World.BlockAccessor.ExchangeBlock(0, Pos);
            return;
        }

        int layer = Math.Max(1, (int)Math.Ceiling(quicklimeCount / (double)BlockQuicklimePile.UnitsPerLayer));
        AssetLocation blockCode = new("immersivequicklime", $"quicklimepile-{layer}");
        Block block = Api.World.GetBlock(blockCode);
        if (block != null && block.Id != 0 && block.Id != Block.Id)
        {
            Api.World.BlockAccessor.ExchangeBlock(block.Id, Pos);
        }

        Api.World.BlockAccessor.MarkBlockDirty(Pos);
        Api.World.BlockAccessor.MarkBlockModified(Pos);
    }

    private int InferCountFromVariant()
    {
        string lastCodePart = Block?.LastCodePart();
        if (!int.TryParse(lastCodePart, out int layer))
        {
            return 0;
        }

        return layer * BlockQuicklimePile.UnitsPerLayer;
    }
}
