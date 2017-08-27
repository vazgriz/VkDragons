using System;
using System.Numerics;

namespace VkDragons {
    public class Transform {
        public Matrix4x4 WorldMatrix { get; private set; }

        Vector3 position;
        public Vector3 Position {
            get {
                return position;
            }
            set {
                position = value;
                Apply();
            }
        }

        Quaternion rotation;
        public Quaternion Rotation {
            get {
                return rotation;
            }
            set {
                rotation = value;
                Apply();
            }
        }

        public Transform() {
            WorldMatrix = Matrix4x4.Identity;
        }

        void Apply() {
            WorldMatrix = Matrix4x4.CreateTranslation(position) * Matrix4x4.CreateFromQuaternion(rotation);
        }
    }
}
