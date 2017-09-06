using System;
using System.Numerics;

using CSGL.GLFW;

namespace VkDragons {
    struct CameraUniform {
        public Matrix4x4 camProjection;
        public Matrix4x4 camView;
        public Matrix4x4 camRotationOnlyView;
        public Matrix4x4 camViewInverse;
    }

    struct LightUniform {
        public Matrix4x4 lightProjection;
        public Matrix4x4 lightView;
        public Vector4 lightPosition;
        public Vector4 lightIa;
        public Vector4 lightId;
        public Vector4 lightIs;
        public float lightShininess;
    }

    public class Scene : IDisposable {
        Renderer renderer;
        Camera camera;
        Input input;

        public Scene(Window window) {
            renderer = new Renderer(window);
            camera = new Camera(45, window.FramebufferWidth, window.FramebufferHeight);
            input = new Input(window, this, renderer, camera);
        }

        public void Dispose() {
            renderer.Dispose();
        }

        public void Resize(int width, int height) {
            renderer.Resize(width, height);
        }
    }
}
