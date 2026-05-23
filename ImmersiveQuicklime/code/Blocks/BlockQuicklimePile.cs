using ImmersiveQuicklime.code.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ImmersiveQuicklime.code.Blocks;

public class BlockQuicklimePile : Block
{
    public const int UnitsPerLayer = 12;
    public const int MaxUnitsPerBlock = 96;

    private static readonly AssetLocation QuicklimeCode = new("game", "quicklime");

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityQuicklimePile be)
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        ItemSlot activeSlot = byPlayer?.InventoryManager?.ActiveHotbarSlot;
        if (activeSlot?.Empty == false && IsQuicklimeItem(activeSlot.Itemstack))
        {
            return be.TryAdd(activeSlot, byPlayer);
        }

        return be.TryTake(byPlayer);
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityQuicklimePile be)
        {
            be.SpillAll();
        }

        world.BlockAccessor.ExchangeBlock(0, pos);
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        Item item = world.GetItem(QuicklimeCode);
        return item == null ? base.OnPickBlock(world, pos) : new ItemStack(item, 1);
    }

    public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
    {
        return "Quicklime Pile";
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        Item quicklimeItem = world.GetItem(QuicklimeCode);
        if (quicklimeItem == null)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }

        return new[]
        {
            new WorldInteraction
            {
                ActionLangCode = "immersivequicklime:blockhelp-addquicklime",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = new[] { new ItemStack(quicklimeItem, 1) }
            },
            new WorldInteraction
            {
                ActionLangCode = "immersivequicklime:blockhelp-takequicklime",
                MouseButton = EnumMouseButton.Right
            }
        };
    }

    public static bool IsQuicklimeItem(ItemStack stack)
    {
        return stack?.Collectible?.Code?.Equals(QuicklimeCode) == true;
    }
}
