using MathLib;

namespace Halo.Camera
{
    /// <summary>
    /// A Camera for converting Halo 5 game coordinates to screen-space
    /// </summary>
    public class Halo5Camera : CameraProjection.Camera
    {
        /// <summary>
        /// The standard Halo 5 field of view
        /// </summary>
        public const float FoVDefault = 50f;

        /// <summary>
        /// Increased Halo 5 field of view when using the Forge FOV filter
        /// </summary>
        public const float FoVOut = 55f;

        /// <summary>
        /// Aspect ratio for 720p/1080p
        /// </summary>
        private const float Aspect = 16f / 9f;

        /// <summary>
        /// Near clipping plane
        /// </summary>
        private const float Near = 0.3f;

        /// <summary>
        /// Far clipping plane
        /// </summary>
        private const float Far = 1000f;

        /// <summary>
        /// Swaps the game's (x, y, z) axes directions to x:right, y:up, z:forward
        /// </summary>
        private static readonly Matrix World = new Matrix(new[]
        {
            new[] { 0f, 0f, 1f, 0f},
            new[] {-1f, 0f, 0f, 0f},
            new[] { 0f,-1f, 0f, 0f},
            new[] { 0f, 0f, 0f, 1f}
        });

        /// <summary>
        /// Create a new Halo 5 Camera using the machinima mode camera properties
        /// The order of the values on screen is shown in brackets
        /// </summary>
        /// <param name="position">The camera's position [1, 2, 3]</param>
        /// <param name="yaw">The camera's yaw [4]</param>
        /// <param name="pitch">The camera's pitch [5]</param>
        /// <param name="fov">The camera's field of view (50 or 55)</param>
        public Halo5Camera(Vector3 position,
                        float yaw,
                        float pitch,
                        float fov = FoVDefault)
                 : base(HaloToWorld(position), HaloToWorld(Direction(yaw, pitch)), fov, Aspect, Near, Far)
        {
        }

        /// <summary>
        /// Project a point from game-space to screen-space
        /// </summary>
        public override ProjectedPoint Project(Vector3 point)
        {
            return base.Project(HaloToWorld(point));
        }

        /// <summary>
        /// Project a point from game-space to screen-space
        /// </summary>
        public override ProjectedLine Project(Vector3 point0, Vector3 point1)
        {
            return base.Project(HaloToWorld(point0), HaloToWorld((point1)));
        }

        /// <summary>
        /// Swaps the game's (x, y, z) axes directions to standard x:right, y:up, z:forward
        /// </summary>
        private static Vector3 HaloToWorld(Vector3 v)
        {
            return v*World;
        }
    }
}
