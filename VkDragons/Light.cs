using System;
using System.Numerics;

namespace VkDragons {
    public class Light {
        public Matrix4x4 Projection { get; private set; }
        public Matrix4x4 View { get; private set; }

        public Vector4 Ia { get; set; }
        public Vector4 Id { get; set; }
        public Vector4 Is { get; set; }
        public float Shininess { get; set; }
        public Vector4 Position { get; set; }

        public Light() {
            var projection = Matrix4x4.CreateOrthographic(1.5f, 1.5f, 2f, 6f);

            projection.M22 *= -1;
            projection.M33 *= 0.5f;
            projection.M34 *= 0.25f;

            Projection = projection;

            Ia = new Vector4(0.3f, 0.3f, 0.3f, 0.3f);
            Id = new Vector4(0.8f, 0.8f, 0.8f, 0);
            Is = new Vector4(1, 1, 1, 0);
            Shininess = 25;

            Update();
        }

        public void Update() {
            View = Matrix4x4.CreateLookAt(new Vector3(Position.X, Position.Y, Position.Z), new Vector3(), new Vector3(0, 1, 0));
        }
    }
}
