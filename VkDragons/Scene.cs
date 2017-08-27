using System;

using CSGL.GLFW;

namespace VkDragons {
    public class Scene : IDisposable {
        Renderer renderer;
        Camera camera;

        public Scene(Window window) {
            renderer = new Renderer(window);
            camera = new Camera(45, window.FramebufferWidth, window.FramebufferHeight);
        }

        public void Dispose() {
            renderer.Dispose();
        }

        public void Resize(int width, int height) {
            renderer.Resize(width, height);
        }
    }
}
