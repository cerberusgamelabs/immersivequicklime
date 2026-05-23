using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ImmersiveQuicklime.code;

public sealed class LimestonePitStructure
{
    public List<BlockPos> Cells { get; } = new();
    public int LimestoneCount { get; set; }
}

public static class LimestonePitStructureResolver
{
    private static readonly AssetLocation LimestoneCode = new("game", "stone-limestone");
    private static readonly AssetLocation LimestoneSandCode = new("game", "sand-limestone");
    private static readonly AssetLocation LimestoneWavySandCode = new("game", "sandwavy-limestone");

    private sealed class BlockPosComparer : IEqualityComparer<BlockPos>
    {
        public static readonly BlockPosComparer Instance = new();

        public bool Equals(BlockPos? x, BlockPos? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.X == y.X && x.Y == y.Y && x.Z == y.Z;
        }

        public int GetHashCode(BlockPos obj)
        {
            return System.HashCode.Combine(obj.X, obj.Y, obj.Z);
        }
    }

    private static readonly BlockFacing[] WalkFaces =
    {
        BlockFacing.NORTH,
        BlockFacing.EAST,
        BlockFacing.SOUTH,
        BlockFacing.WEST,
        BlockFacing.UP,
        BlockFacing.DOWN
    };

    public static bool TryResolve(ICoreAPI api, BlockPos rootPos, BlockPos gratePos, int maxDimension, int maxHeight, out LimestonePitStructure structure, out string failureCode)
    {
        structure = new LimestonePitStructure();
        failureCode = "";

        BlockPos? startPos = FindStartPos(api.World, rootPos);
        if (startPos == null)
        {
            failureCode = "immersivequicklime:error-no-limestone";
            return false;
        }

        var queue = new Queue<BlockPos>();
        var visited = new HashSet<BlockPos>(BlockPosComparer.Instance);
        var minPos = startPos.Copy();
        var maxPos = startPos.Copy();

        queue.Enqueue(startPos.Copy());
        visited.Add(startPos.Copy());

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
        if (!TryGetLimestoneCell(api.World, pos, out int count))
        {
            continue;
        }

            structure.Cells.Add(pos.Copy());
            structure.LimestoneCount += count;

            foreach (var face in WalkFaces)
            {
                var next = pos.AddCopy(face);
                if (visited.Contains(next))
                {
                    continue;
                }

                if (!TryGetLimestoneCell(api.World, next, out _))
                {
                    continue;
                }

                var nextMin = minPos.Copy();
                var nextMax = maxPos.Copy();
                UpdateBounds(next, ref nextMin, ref nextMax);
                if (!WithinBounds(nextMin, nextMax, maxDimension, maxHeight))
                {
                    failureCode = "immersivequicklime:error-too-large";
                    return false;
                }

                minPos = nextMin;
                maxPos = nextMax;
                visited.Add(next.Copy());
                queue.Enqueue(next.Copy());
            }
        }

        if (structure.Cells.Count == 0)
        {
            failureCode = "immersivequicklime:error-no-limestone";
            return false;
        }

        if (!ValidateSealedPit(api.World, structure, gratePos, out failureCode))
        {
            return false;
        }

        return true;
    }

    public static bool TryGetLimestoneCell(IWorldAccessor world, BlockPos pos, out int limestoneCount)
    {
        limestoneCount = 0;
        Block block = world.BlockAccessor.GetBlock(pos);
        if (TryGetLimestoneSandEquivalent(block, out limestoneCount))
        {
            return true;
        }

        BlockEntityGroundStorage? storage = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGroundStorage;
        if (storage == null)
        {
            return false;
        }

        for (int i = 0; i < storage.Inventory.Count; i++)
        {
            var slot = storage.Inventory[i];
            if (slot.Empty)
            {
                continue;
            }

            if (!slot.Itemstack.Collectible.Code.Equals(LimestoneCode))
            {
                return false;
            }

            limestoneCount += slot.StackSize;
        }

        return limestoneCount > 0;
    }

    private static bool TryGetLimestoneSandEquivalent(Block? block, out int limestoneCount)
    {
        limestoneCount = 0;
        AssetLocation? code = block?.Code;
        if (code == null || code.Domain != "game")
        {
            return false;
        }

        if (code.Equals(LimestoneSandCode) || code.Equals(LimestoneWavySandCode))
        {
            limestoneCount = 64;
            return true;
        }

        const string layeredPrefix = "sand-limestone-";
        if (!code.Path.StartsWith(layeredPrefix))
        {
            return false;
        }

        string layerText = code.Path[layeredPrefix.Length..];
        if (!int.TryParse(layerText, out int layer) || layer < 1 || layer > 7)
        {
            return false;
        }

        limestoneCount = layer * 8;
        return true;
    }

    private static bool ValidateSealedPit(IWorldAccessor world, LimestonePitStructure structure, BlockPos gratePos, out string failureCode)
    {
        failureCode = "";
        var cellSet = new HashSet<BlockPos>(structure.Cells, BlockPosComparer.Instance);

        foreach (var cell in structure.Cells)
        {
            if (!IsSupportedNonCombustible(world, cell.DownCopy()))
            {
                failureCode = "immersivequicklime:error-bad-floor";
                return false;
            }

            foreach (var face in BlockFacing.HORIZONTALS)
            {
                var sidePos = cell.AddCopy(face);
                if (cellSet.Contains(sidePos))
                {
                    continue;
                }

                if (!IsNonCombustibleKilnBlock(world, sidePos))
                {
                    failureCode = "immersivequicklime:error-bad-wall";
                    return false;
                }
            }

            var up = cell.UpCopy();
            if (cellSet.Contains(up))
            {
                continue;
            }

            if (up.Equals(gratePos))
            {
                var block = world.BlockAccessor.GetBlock(gratePos);
                if (block.Code == null || block.Code.Domain != "immersivequicklime" || !block.Code.Path.StartsWith("domedgrate"))
                {
                    failureCode = "immersivequicklime:error-no-grate";
                    return false;
                }

                continue;
            }

            if (!IsNonCombustibleKilnBlock(world, up))
            {
                failureCode = "immersivequicklime:error-unsealed";
                return false;
            }
        }

        return true;
    }

    private static BlockPos? FindStartPos(IWorldAccessor world, BlockPos rootPos)
    {
        if (TryGetLimestoneCell(world, rootPos, out _))
        {
            return rootPos.Copy();
        }

        foreach (var face in WalkFaces)
        {
            var pos = rootPos.AddCopy(face);
            if (TryGetLimestoneCell(world, pos, out _))
            {
                return pos;
            }
        }

        return null;
    }

    private static bool IsNonCombustibleKilnBlock(IWorldAccessor world, BlockPos pos)
    {
        var block = world.BlockAccessor.GetBlock(pos);
        if (block == null || block.BlockId == 0)
        {
            return false;
        }

        return block.GetCombustibleProperties(world, null, pos) == null;
    }

    private static bool IsSupportedNonCombustible(IWorldAccessor world, BlockPos pos)
    {
        var block = world.BlockAccessor.GetBlock(pos);
        if (block == null || block.BlockId == 0)
        {
            return false;
        }

        if (block.GetCombustibleProperties(world, null, pos) != null)
        {
            return false;
        }

        return block.CanAttachBlockAt(world.BlockAccessor, block, pos, BlockFacing.UP) || block.SideSolid[BlockFacing.UP.Index];
    }

    private static void UpdateBounds(BlockPos pos, ref BlockPos minPos, ref BlockPos maxPos)
    {
        minPos.Set(
            System.Math.Min(minPos.X, pos.X),
            System.Math.Min(minPos.Y, pos.Y),
            System.Math.Min(minPos.Z, pos.Z)
        );
        maxPos.Set(
            System.Math.Max(maxPos.X, pos.X),
            System.Math.Max(maxPos.Y, pos.Y),
            System.Math.Max(maxPos.Z, pos.Z)
        );
    }

    private static bool WithinBounds(BlockPos minPos, BlockPos maxPos, int maxDimension, int maxHeight)
    {
        return maxPos.X - minPos.X + 1 <= maxDimension
            && maxPos.Y - minPos.Y + 1 <= maxHeight
            && maxPos.Z - minPos.Z + 1 <= maxDimension;
    }
}
