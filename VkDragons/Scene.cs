using System;

using CSGL.GLFW;

namespace VkDragons {
    public class Scene : IDisposable {
        Renderer renderer;

        public Scene(Window window) {
            renderer = new Renderer(window);
        }

        public void Dispose() {
            renderer.Dispose();
        }
    }
}
