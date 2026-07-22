namespace RaccoonHeist.World
{
    // Fixed world-scale constants from CLAUDE.md. 1 Unity unit = 1 meter.
    // Layout code derives everything from these — change here, regenerate the shop.
    public static class ShopConstants
    {
        // Main shop footprint (x = width, z = depth). Front wall is at z = 0.
        public const float ShopWidth = 14f;
        public const float ShopDepth = 16f;
        public const float CeilingHeight = 3f;
        public const float WallThickness = 0.2f;

        // Back room (Harold's), attached behind the main shop. ONE doorway only.
        public const float BackRoomWidth = 3f;
        public const float BackRoomDepth = 4f;
        public const float DoorwayWidth = 0.9f;
        public const float DoorwayHeight = 2.1f;

        // Storage room behind the east side of the shop, open doorway (no door)
        public const float StorageWidth = 6f;
        public const float StorageDepth = 5f;

        // Shelving
        public const float ShelfLength = 2f;
        public const float ShelfDepth = 0.5f;
        public const float ShelfHeight = 1.8f;
        public const float AisleWidth = 1.2f;

        // Characters
        public const float RaccoonHeight = 0.5f;
        public const float RaccoonCrouchHeight = 0.3f;
        public const float RaccoonEyeHeight = 0.3f;
        public const float HaroldHeight = 1.8f;
    }
}
