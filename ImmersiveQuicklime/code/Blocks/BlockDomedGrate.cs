using System.Collections.Generic;
using System.Linq;
using ImmersiveQuicklime.code.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ImmersiveQuicklime.code.Blocks;

public class BlockDomedGrate : Block, IIgnitable
{
    private static readonly AssetLocation OffVariantCode = new("immersivequicklime", "domedgrate-off");

    public LimestoneKilnConfig KilnConfig { get; private set; } = new();

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        KilnConfig = Attributes?["kilnConfig"].AsObject<LimestoneKilnConfig>() ?? new LimestoneKilnConfig();
        KilnConfig.MaxDimension = GameMath.Clamp(KilnConfig.MaxDimension, 1, 99);
        KilnConfig.MaxHeight = GameMath.Clamp(KilnConfig.MaxHeight, 1, 99);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        return (world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityDomedGrate)?.OnInteract(byPlayer) ?? false;
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        Block offVariant = world.GetBlock(OffVariantCode);
        return offVariant == null ? base.OnPickBlock(world, pos) : new ItemStack(offVariant);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        var be = world.BlockAccessor.GetBlockEntity(selection.Position) as BlockEntityDomedGrate;
        var interactions = new List<WorldInteraction>();

        if (be?.Lit != true)
        {
            var fuelStacks = new List<ItemStack>();
            foreach (var fuel in KilnConfig.Fuels)
            {
                CollectibleObject collectible = fuel.Type == "block"
                    ? world.GetBlock(AssetLocation.Create(fuel.Code))
                    : world.GetItem(AssetLocation.Create(fuel.Code));

                if (collectible != null)
                {
                    fuelStacks.Add(new ItemStack(collectible));
                }
            }

            if (fuelStacks.Count > 0)
            {
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = "immersivequicklime:blockhelp-addfuel",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = fuelStacks.ToArray()
                });
            }
        }

        interactions.Add(new WorldInteraction
        {
            ActionLangCode = "blockhelp-firepit-ignite",
            MouseButton = EnumMouseButton.Right,
            HotKeyCode = "shift",
            Itemstacks = BlockBehaviorCanIgnite.CanIgniteStacks(world.Api, true).ToArray()
        });

        return interactions.ToArray();
    }

    EnumIgniteState IIgnitable.OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
    {
        var be = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityDomedGrate;
        if (be == null || !be.CanIgnite())
        {
            return EnumIgniteState.NotIgnitablePreventDefault;
        }

        return secondsIgniting > 4 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
    }

    void IIgnitable.OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;
        (byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityDomedGrate)?.TryIgnite((byEntity as EntityPlayer)?.Player);
    }

    EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
    {
        return EnumIgniteState.NotIgnitablePreventDefault;
    }
}
