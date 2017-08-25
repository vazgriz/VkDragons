using System;

namespace VkDragons {
    public class Scene : IDisposable {
        Renderer renderer;

        public Scene(int width, int height) {
            renderer = new Renderer(width, height);
        }

        public void Dispose() {
            renderer.Dispose();
        }
    }
}
