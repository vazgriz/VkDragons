﻿using System;

using CSGL.GLFW;

namespace VkDragons {
    class Program {
        static void Main(string[] args) {
            new Program().Run();
        }

        void Run() {
            GLFW.Init();
            GLFW.WindowHint(WindowHint.ClientAPI, (int)ClientAPI.NoAPI);
            GLFW.WindowHint(WindowHint.Visible, 0);

            Window window = new Window(800, 600, "Here be Dragons");

            using (Scene scene = new Scene(window)) {
                window.Visible = true;
                while (!window.ShouldClose) {
                    GLFW.PollEvents();
                }
            }

            GLFW.Terminate();
        }
    }
}
