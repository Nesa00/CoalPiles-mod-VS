using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CoalPiles
{
    public class CoalPilesModSystem : ModSystem
    {
        public const bool DebugChat = false;
        public const string PileBlockCode = "coalpiles:densecoalpile";
        public const string DefaultFuelCode = "charcoal";

        public static readonly string[] SupportedFuelPaths =
        {
            "charcoal",
            "ore-bituminouscoal",
            "ore-lignite",
            "ore-anthracite",
            "coke",
            "coal-contaminated"
        };

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("BlockDenseCoalPile", typeof(BlockCoalPile));
            api.RegisterBlockEntityClass("DenseCoalPile", typeof(BlockEntityCoalPile));
            api.RegisterCollectibleBehaviorClass("DenseCoalPilePlace", typeof(CoalPilePlaceBehavior));
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);

            foreach (string path in SupportedFuelPaths)
            {
                Item item = GetFuelItem(api.World, path);
                if (item == null) continue;

                var behaviors = item.CollectibleBehaviors.ToList();
                behaviors.Insert(0, new CoalPilePlaceBehavior(item));
                item.CollectibleBehaviors = behaviors.ToArray();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            BlockEntityCoalPile.ClearMeshCache();
        }

        public static Item GetFuelItem(IWorldAccessor world, string fuelCode)
        {
            return world?.GetItem(new AssetLocation("game:" + fuelCode));
        }

        public static string GetFuelName(IWorldAccessor world, string fuelCode)
        {
            Item item = GetFuelItem(world, fuelCode);
            return item != null ? new ItemStack(item).GetName() : fuelCode;
        }

        public static void LogChat(IWorldAccessor world, string message)
        {
            if (DebugChat && world.Api is ICoreClientAPI capi)
            {
                capi.ShowChatMessage($"[CoalPiles] {message}");
            }
        }
    }
}