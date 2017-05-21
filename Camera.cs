using System;
using System.Collections.Generic;
using System.Linq;
using MathLib;

namespace CameraProjection
{
    /// <summary>
    /// Projects points and lines from a perspective projection camera to screenspace coordinates
    /// </summary>
    public class Camera
    {
        /// <summary>
        /// The 3D Position of the camera
        /// </summary>
        public readonly Vector3 Position;

        /// <summary>
        /// The direction the camera is looking
        /// </summary>
        public readonly Vector3 Forward;

        /// <summary>
        /// The up vector with respect to the direction the camera is looking
        /// </summary>
        public readonly Vector3 Up;

        /// <summary>
        /// The camera's aspect ratio
        /// </summary>
        private readonly float _aspect;

        /// <summary>
        /// The camera's field of view
        /// </summary>
        private readonly float _foV;

        /// <summary>
        /// The camera's near plane
        /// </summary>
        private readonly float _near;

        /// <summary>
        /// The camera's far plane
        /// </summary>
        private readonly float _far;

        /// <summary>
        /// Matrix to project from world to screen space
        /// </summary>
        private readonly Matrix _viewMatrix;

        public Camera(Vector3 position,
                      float yaw,
                      float pitch,
                      float foV,
                      float aspect,
                      float near,
                      float far)
               : this(position,
                      Direction(yaw, pitch),
                      foV,
                      aspect,
                      near,
                      far)
        {
        }

        public Camera(Vector3 position,
                      Vector3 forward,
                      float foV,
                      float aspect,
                      float near,
                      float far)
              : this(position,
                      forward,
                      UpFromForward(forward),
                      foV,
                      aspect,
                      near,
                      far)
        {
        }

        public Camera(Vector3 position,
                      Vector3 forward,
                      Vector3 up,
                      float foV,
                      float aspect,
                      float near,
                      float far)
        {
            Position = position;
            Forward = forward;
            Up = up;
            _foV = foV;
            _aspect = aspect;
            _near = near;
            _far = far;
            _viewMatrix = CameraViewMatrix();
        }

        /// <summary>
        /// A point projected to screen-space
        /// </summary>
        public class ProjectedPoint
        {
            public enum PointVisibility
            {
                Visible,
                Clipped
            }

            public PointVisibility Visibility;
            public Vector3 Position;
            public float Distance;
        }

        /// <summary>
        /// A line projected to screen-space
        /// </summary>
        public class ProjectedLine
        {
            public enum LineVisibility
            {
                Visible,
                Partial,
                Clipped
            }

            public LineVisibility Visibility;
            public ProjectedPoint Point0;
            public ProjectedPoint Point1;
            public Vector3 Intersect;
        }

        /// <summary>
        /// Project a collection of points from word-space to screen-space
        /// </summary>
        public IEnumerable<ProjectedPoint> Project(IEnumerable<Vector3> points)
        {
            return points.Select(p => Project(p));
        }

        /// <summary>
        /// Project a point from word-space to screen-space
        /// </summary>
        public virtual ProjectedPoint Project(Vector3 point)
        {
            var result = new ProjectedPoint
            {
                Distance = Vector3.Distance(Position, point),
            };

            // Project point to cube space
            var projection = Matrix.Multiply(point, _viewMatrix).DivideByW();

            // Clip points outside of view
            if (Clip(projection))
                result.Visibility = ProjectedPoint.PointVisibility.Clipped;

            // Fit results between 0-1
            result.Position = Matrix.Multiply(projection, ViewPortMatrix).ToVector3();

            return result;
        }

        /// <summary>
        /// Project a line from word-space to screen-space
        /// </summary>
        public virtual ProjectedLine Project(Vector3 point0, Vector3 point1)
        {
            // Project points to perspective projection space
            var projected0 = Matrix.Multiply(point0, _viewMatrix).ToVector4();
            var projected1 = Matrix.Multiply(point1, _viewMatrix).ToVector4();

            // Clip line and project to cube space (divide by w)
            var projectedLine = CohenSutherlandLineClip3D(projected0, projected1);

            // Fit results between 0-1
            projectedLine.Point0.Position = Matrix.Multiply(projectedLine.Point0.Position, ViewPortMatrix).ToVector3();
            projectedLine.Point1.Position = Matrix.Multiply(projectedLine.Point1.Position, ViewPortMatrix).ToVector3();

            // Distance from camera to points
            projectedLine.Point0.Distance = Vector3.Distance(Position, point0);
            projectedLine.Point1.Distance = Vector3.Distance(Position, point1);

            return projectedLine;
        }

        /// <summary>
        /// Calculate the camera's direction from the yaw and pitch
        /// </summary>
        public static Vector3 Direction(float yaw, float pitch)
        {
            yaw = MathF.Radians(yaw);
            pitch = MathF.Radians(pitch);

            var x = MathF.Cos(yaw) * MathF.Cos(pitch);
            var y = MathF.Sin(yaw) * MathF.Cos(pitch);
            var z = MathF.Sin(pitch);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Calculate an Up vector from a Forward direction (assuming 0 roll)
        /// </summary>
        private static Vector3 UpFromForward(Vector3 forward)
        {
            return Vector3.Cross(Vector3.Cross(forward, Vector3.Up), forward);
        }

        #region Matrices

        /// <summary>
        /// Creates a camera view matrix to convert points from world-space to screen-space
        /// </summary>
        /// <param name="position">Camera position</param>
        /// <param name="forward">Camera forward direction</param>
        /// <param name="up">Camera up direction</param>
        /// <param name="near">Camera near plane</param>
        /// <param name="far">Camera far plane</param>
        /// <param name="foV">Camera field of view</param>
        /// <param name="aspect">Camera aspect ratio</param>
        /// <returns></returns>
        protected static Matrix CreateViewMatrix(Vector3 position, Vector3 forward, Vector3 up, float near, float far,
            float foV, float aspect)
        {
            var t = TranslationMatrix(position);
            var r = RotationMatrix(forward, up);
            var p = PerspectiveMatrix(foV, aspect, near, far);

            return t * r * p;
        }

        /// <summary>
        /// Create a view matrix for this camera
        /// </summary>
        /// <returns></returns>
        protected Matrix CameraViewMatrix()
        {
            return CreateViewMatrix(Position, Forward, Up, _near, _far, _foV, _aspect);
        }

        /// <summary>
        /// Translates a point to the origin
        /// </summary>
        protected static Matrix TranslationMatrix(Vector3 p)
        {
            var translation = new Matrix(new[]
            {
                new[] { 1f, 0f, 0f, 0f},
                new[] { 0f, 1f, 0f, 0f},
                new[] { 0f, 0f, 1f, 0f},
                new[] {-p.X,-p.Y,-p.Z, 1f}
            });

            return translation;
        }

        /// <summary>
        /// Creates a rotation matrix
        /// </summary>
        protected static Matrix RotationMatrix(Vector3 forward, Vector3 up)
        {
            // Rotation about Y axis
            var y = MathF.Atan2(forward.X, forward.Z);
            // Rotation about X axis
            var x = MathF.Asin(-forward.Y);

            var rotationX = new Matrix(new[]
            {
                new[] {1f, 0f, 0f, 0f},
                new[] {0f, MathF.Cos(x),-MathF.Sin(x), 0f},
                new[] {0f, MathF.Sin(x), MathF.Cos(x), 0f},
                new[] {0f, 0f, 0f, 1f}
            });

            var rotationY = new Matrix(new[]
            {
                new[] { MathF.Cos(y), 0f, MathF.Sin(y), 0f},
                new[] {0f, 1f, 0f, 0f},
                new[] {-MathF.Sin(y), 0f, MathF.Cos(y), 0f},
                new[] {0f, 0f, 0f, 1f}
            });

            //var rotationZ = new Matrix(new[]
            //{
            //    new[] {Mathf.Cos(z), -Mathf.Sin(z), 0f, 0f},
            //    new[] {Mathf.Sin(z), Mathf.Cos(z), 0f, 0f},
            //    new[] {0f, 0f, 1f, 0f},
            //    new[] {0f, 0f, 0f, 1f}
            //});

            return rotationY * rotationX;
        }

        /// <summary>
        /// Creates a perspective matrix
        /// </summary>
        /// <param name="fov">Field of view</param>
        /// <param name="aspect">Aspect ratio</param>
        /// <param name="n">Near plane</param>
        /// <param name="f">Far plane</param>
        /// <returns></returns>
        protected static Matrix PerspectiveMatrix(float fov, float aspect, float n, float f)
        {
            fov = MathF.Radians(fov);

            var a = (f + n) / (f - n);
            var b = (2 * n * f) / (f - n);

            var yScale = MathF.Cot(fov / 2);
            var xScale = yScale / aspect;

            var m = new Matrix(new[]
            {
                new[] { xScale,     0f, 0f, 0f },
                new[] {     0f, yScale, 0f, 0f },
                new[] {     0f,     0f, a, 1f },
                new[] {     0f,     0f, b, 0f }
            });

            return m;
        }

        /// <summary>
        /// Converts the screenspace coordinates to range [0-1]
        /// </summary>
        private static readonly Matrix ViewPortMatrix = new Matrix(new[]
        {
            new[] {0.5f,   0f, 0f, 0f},
            new[] {  0f, 0.5f, 0f, 0f},
            new[] {  0f,   0f, 0f, 0f},
            new[] {0.5f, 0.5f, 0f, 0f}
        });

        #endregion

        #region Clipping

        /// <summary>
        /// Simple screenspace clipping
        /// </summary>
        private static bool Clip(Vector3 p)
        {
            return p.X < -1 || p.X > 1 || p.Y < -1 || p.Y > 1 || p.Z < 0;
        }

        /// <summary>
        /// Algorithm for clipping lines infront of the camera in screenspace.
        /// Does not work for points behind the camera
        /// </summary>
        #region Cohen–Sutherland 2D line clipping algorithm

        [Flags]
        private enum OutCode
        {
            Inside = 0,
            Left = 1,
            Right = 2,
            Bottom = 4,
            Top = 8
        }

        private const float XMin = 0;
        private const float XMax = 1;
        private const float YMin = 0;
        private const float YMax = 1;

        private static OutCode ComputeOutCode(float x, float y)
        {
            var code = OutCode.Inside;

            if (x < XMin)
                code |= OutCode.Left;
            else if (x > XMax)
                code |= OutCode.Right;

            if (y < YMin)
                code |= OutCode.Bottom;
            else if (y > YMax)
                code |= OutCode.Top;

            return code;
        }

        protected static ProjectedLine CohenSutherlandLineClip(Vector3 v0, Vector3 v1)
        {
            return CohenSutherlandLineClip(v0.X, v0.Y, v1.X, v1.Y);
        }

        /// <summary>
        /// Clip a line to the viewport
        /// </summary>
        protected static ProjectedLine CohenSutherlandLineClip(float x0, float y0, float x1, float y1)
        {
            var code0 = ComputeOutCode(x0, y0);
            var code1 = ComputeOutCode(x1, y1);

            float x = 0;
            float y = 0;

            var point0Visibility = code0 == OutCode.Inside
                ? ProjectedPoint.PointVisibility.Visible
                : ProjectedPoint.PointVisibility.Clipped;

            var point1Visibility = code1 == OutCode.Inside
                ? ProjectedPoint.PointVisibility.Visible
                : ProjectedPoint.PointVisibility.Clipped;

            var lineVisibility = ProjectedLine.LineVisibility.Visible;

            while (true)
            {
                // Both inside
                if ((code0 | code1) == 0)
                {
                    break;
                }
                // Both outside
                if ((code0 & code1) != 0)
                {
                    lineVisibility = ProjectedLine.LineVisibility.Clipped;
                    break;
                }

                lineVisibility = ProjectedLine.LineVisibility.Partial;

                var codeOut = code0 != OutCode.Inside ? code0 : code1;

                if ((codeOut & OutCode.Top) != 0)
                {
                    x = x0 + (x1 - x0) * (YMax - y0) / (y1 - y0);
                    y = YMax;
                }
                else if ((codeOut & OutCode.Bottom) != 0)
                {
                    x = x0 + (x1 - x0) * (YMin - y0) / (y1 - y0);
                    y = YMin;
                }
                else if ((codeOut & OutCode.Right) != 0)
                {
                    y = y0 + (y1 - y0) * (XMax - x0) / (x1 - x0);
                    x = XMax;
                }
                else if ((codeOut & OutCode.Left) != 0)
                {
                    y = y0 + (y1 - y0) * (XMin - x0) / (x1 - x0);
                    x = XMin;
                }

                if (codeOut == code0)
                {
                    x0 = x;
                    y0 = y;
                    code0 = ComputeOutCode(x0, y0);
                }
                else
                {
                    x1 = x;
                    y1 = y;
                    code1 = ComputeOutCode(x1, y1);
                }
            }

            return new ProjectedLine()
            {
                Point0 = new ProjectedPoint()
                {
                    Position = new Vector3(x0, y0, 0),
                    Visibility = point0Visibility
                },
                Point1 = new ProjectedPoint()
                {
                    Position = new Vector3(x1, y1, 0),
                    Visibility = point1Visibility
                },
                Visibility = lineVisibility
            };
        }

        #endregion

        /// <summary>
        /// Algorithm for clipping lines infront of the camera in screenspace.
        /// </summary>
        #region Cohen–Sutherland 3D line clipping algorithm

        [Flags]
        private enum OutCode3D
        {
            Inside = 0,
            Top = 1,
            Bottom = 2,
            Right = 4,
            Left = 8,
            Near = 16,
            Far = 32
        }

        private OutCode3D ComputeOutCode3D(Vector4 v)
        {
            return ComputeOutCode3D(v.X, v.Y, v.Z);
        }

        private OutCode3D ComputeOutCode3D(float x, float y, float z)
        {
            var code = OutCode3D.Inside;

            if (y > z)
                code |= OutCode3D.Top;
            else if (y < -z)
                code |= OutCode3D.Bottom;

            if (x > z)
                code |= OutCode3D.Right;
            else if (x < -z)
                code |= OutCode3D.Left;

            if (z < 0)
                code |= OutCode3D.Near;
            else if (z > _far)
                code |= OutCode3D.Far;

            return code;
        }

        private ProjectedLine CohenSutherlandLineClip3D(Vector4 v0, Vector4 v1)
        {
            return CohenSutherlandLineClip3D(v0.X, v0.Y, v0.Z, v0.W, v1.X, v1.Y, v1.Z, v1.W);
        }

        /// <summary>
        /// Clip a line to the viewport
        /// </summary>
        private ProjectedLine CohenSutherlandLineClip3D(float x0, float y0, float z0, float w0, float x1,
            float y1, float z1, float w1)
        {
            var code0 = ComputeOutCode3D(x0, y0, z0);
            var code1 = ComputeOutCode3D(x1, y1, z1);

            var pointVisibility0 = code0 == OutCode3D.Inside
                ? ProjectedPoint.PointVisibility.Visible
                : ProjectedPoint.PointVisibility.Clipped;

            var pointVisibility1 = code1 == OutCode3D.Inside
                ? ProjectedPoint.PointVisibility.Visible
                : ProjectedPoint.PointVisibility.Clipped;

            var lineVisibility = ProjectedLine.LineVisibility.Visible;

            float x = 0;
            float y = 0;
            float z = 0;
            float w = 0;

            float t = 0;
            float tMin = 0;
            float tMax = 1;

            var n = 0;

            while (true)
            {
                // Both inside
                if ((code0 | code1) == 0)
                {
                    break;
                }
                // Both outside
                if ((code0 & code1) != 0)
                {
                    lineVisibility = ProjectedLine.LineVisibility.Clipped;
                    break;
                }

                lineVisibility = ProjectedLine.LineVisibility.Partial;

                var codeOut = code0 != OutCode3D.Inside ? code0 : code1;

                if ((codeOut & OutCode3D.Top) != 0)
                {
                    t = (z0 - y0) / ((y1 - y0) - (z1 - z0));

                    z = z0 + (z1 - z0) * t;
                    x = x0 + (x1 - x0) * t;
                    y = z;
                    w = w0 + (w1 - w0) * t;
                }
                else if ((codeOut & OutCode3D.Bottom) != 0)
                {
                    t = (-z0 - y0) / ((y1 - y0) + (z1 - z0));

                    z = z0 + (z1 - z0) * t;
                    x = x0 + (x1 - x0) * t;
                    y = -z;
                    w = w0 + (w1 - w0) * t;
                }
                else if ((codeOut & OutCode3D.Right) != 0)
                {
                    t = (z0 - x0) / ((x1 - x0) - (z1 - z0));

                    z = z0 + (z1 - z0) * t;
                    x = z;
                    y = y0 + (y1 - y0) * t;
                    w = w0 + (w1 - w0) * t;
                }
                else if ((codeOut & OutCode3D.Left) != 0)
                {
                    t = (-z0 - x0) / ((x1 - x0) + (z1 - z0));

                    z = z0 + (z1 - z0) * t;
                    x = -z;
                    y = y0 + (y1 - y0) * t;
                    w = w0 + (w1 - w0) * t;
                }
                else if ((codeOut & OutCode3D.Near) != 0)
                {
                    t = z0 / (z0 - z1);

                    z = z0 + (z1 - z0) * t;
                    //z = 0;
                    x = x0 + (x1 - x0) * t;
                    y = y0 + (y1 - y0) * t;
                    w = w0 + (w1 - w0) * t;
                }
                else if ((codeOut & OutCode3D.Far) != 0)
                {
                    t = (z0 - w0) / ((z0 - w0) - (z1 - w1));

                    z = _far;
                    x = x0 + (x1 - x0) * t;
                    y = y0 + (y1 - y0) * t;
                    w = w0 + (w1 - w0) * t;
                }

                if (codeOut == code0)
                {
                    x0 = x;
                    y0 = y;
                    z0 = z;
                    w0 = w;

                    if (t > tMin)
                        tMin = t;

                    code0 = ComputeOutCode3D(x0, y0, z0);
                }
                else
                {
                    x1 = x;
                    y1 = y;
                    z1 = z;
                    w1 = w;

                    if (t < tMax)
                        tMax = t;

                    code1 = ComputeOutCode3D(x1, y1, z1);
                }

                // Avoid infinite loop if something goes horribly wrong
                if (n++ > 30)
                    break;
            }

            return new ProjectedLine()
            {
                Point0 = new ProjectedPoint()
                {
                    Position = new Vector3(x0 / w0, y0 / w0, z0 / w0),
                    Visibility = pointVisibility0
                },
                Point1 = new ProjectedPoint()
                {
                    Position = new Vector3(x1 / w1, y1 / w1, z0 / w0),
                    Visibility = pointVisibility1
                },
                Visibility = lineVisibility
            };
        }

        #endregion

        #endregion
    }
}
