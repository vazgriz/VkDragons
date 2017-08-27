using System;

using CSGL.GLFW;

namespace VkDragons {
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
