using System;

using CSGL.GLFW;

namespace VkDragons {
    class Program {
        bool resizedFlag;
        int width;
        int height;

        static void Main(string[] args) {
            new Program().Run();
        }

        void Run() {
            GLFW.Init();
            GLFW.WindowHint(WindowHint.ClientAPI, (int)ClientAPI.NoAPI);
            GLFW.WindowHint(WindowHint.Visible, 0);

            Window window = new Window(800, 600, "Here be Dragons");
            window.OnSizeChanged += OnResize;

            using (Scene scene = new Scene(window)) {
                window.Visible = true;
                double lastTime = 0;
                while (!window.ShouldClose) {
                    GLFW.PollEvents();

                    if (resizedFlag) {
                        scene.Resize(width, height);
                        resizedFlag = false;
                    }

                    double now = GLFW.GetTime();
                    double elapsed = now - lastTime;

                    scene.Update(elapsed);
                    scene.Render();
                }
            }

            GLFW.Terminate();
        }

        void OnResize(int width, int height) {
            resizedFlag = true;
            this.width = width;
            this.height = height;
        }
    }
}
