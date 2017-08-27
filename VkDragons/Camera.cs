using System;
using System.Numerics;

namespace VkDragons {
    public class Camera {
        public Matrix4x4 View { get; private set; }
        public Matrix4x4 RotationOnlyView { get; private set; }
        public Matrix4x4 Projection { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public float FOV { get; set; }

        //https://matthewwellings.com/blog/the-new-vulkan-coordinate-system/
        Matrix4x4 correctionMatrix = new Matrix4x4(
            1, 0, 0, 0,
            0, -1, 0, 0,
            0, 0, 0.5f, 0.5f,
            0, 0, 0, 1
        );

        public Camera(float fov, int width, int height) {
            FOV = fov;
            Width = width;
            Height = height;

            Rotation = Quaternion.Identity;
        }

        public void SetSize(int width, int height) {
            Width = width;
            Height = height;
        }

        public void Update() {
            Projection = Matrix4x4.CreatePerspectiveFieldOfView(FOV, Width / (float)Height, 0.1f, 100);
            Projection = correctionMatrix * Projection;

            Vector3 forward = Vector3.Transform(new Vector3(0, 0, -1), Rotation);
            Vector3 up = Vector3.Transform(new Vector3(0, 1, 0), Rotation);
            View = Matrix4x4.CreateLookAt(Position, Position + forward, up);
            RotationOnlyView = Matrix4x4.CreateLookAt(new Vector3(), forward, up);
        }
    }
}
