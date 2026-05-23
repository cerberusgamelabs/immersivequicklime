using ImmersiveQuicklime.code.BlockEntities;
using ImmersiveQuicklime.code.Blocks;
using Vintagestory.API.Common;

namespace ImmersiveQuicklime.code;

public class ImmersiveQuicklimeModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("BlockDomedGrate", typeof(BlockDomedGrate));
        api.RegisterBlockClass("BlockQuicklimePile", typeof(BlockQuicklimePile));
        api.RegisterBlockEntityClass("DomedLimestoneKiln", typeof(BlockEntityDomedGrate));
        api.RegisterBlockEntityClass("QuicklimePile", typeof(BlockEntityQuicklimePile));
    }
}
