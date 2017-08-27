using System;
using System.Numerics;

using CSGL.Input;
using CSGL.GLFW;

namespace VkDragons {
    public class Input {
        Window window;
        Scene scene;
        Renderer renderer;
        Camera camera;

        bool forward;
        bool back;
        bool right;
        bool left;
        bool up;
        bool down;
        bool looking;

        float mouseX;
        float mouseY;
        float lookX;
        float lookY;

        public Input(Window window, Scene scene, Renderer renderer, Camera camera) {
            this.window = window;
            this.scene = scene;
            this.renderer = renderer;
            this.camera = camera;

            window.OnKey += HandleKey;
            window.OnCursorPos += HandleMouse;
            window.OnMouseButton += HandleMouseButton;
        }

        void Toggle(ref bool state, KeyCode keyCode, KeyCode input, KeyAction action) {
            if (keyCode == input) {
                if (action == KeyAction.Press) {
                    state = true;
                } else if (action == KeyAction.Release) {
                    state = false;
                }
            }
        }

        void HandleKey(KeyCode key, int scancode, KeyAction action, KeyMod modifier) {
            Toggle(ref forward, KeyCode.W, key, action);
            Toggle(ref back, KeyCode.S, key, action);
            Toggle(ref right, KeyCode.D, key, action);
            Toggle(ref left, KeyCode.A, key, action);
            Toggle(ref up, KeyCode.E, key, action);
            Toggle(ref down, KeyCode.Q, key, action);

            if (key == KeyCode.Space && action == KeyAction.Press) {
                renderer.VSync = !renderer.VSync;
                scene.Resize(renderer.Width, renderer.Height);
            }
        }

        void HandleMouse(double x, double y) {
            if (!looking) return;
            float newMouseX = (float)x;
            float newMouseY = (float)y;

            float deltaX = newMouseX - mouseX;
            float deltaY = newMouseY - mouseY;

            lookX += deltaX / 8;
            lookY += deltaY / 8;
            lookY = Math.Min(Math.Max(lookY, -90), 90);

            mouseX = newMouseX;
            mouseY = newMouseY;
        }

        void HandleMouseButton(MouseButton button, KeyAction action, KeyMod modifiers) {
            if (button == MouseButton.Button1) {
                if (action == KeyAction.Press) {
                    window.CursorMode = CursorMode.Disabled;
                    looking = true;
                    double x;
                    double y;
                    GLFW.GetCursorPos(window.Native, out x, out y);
                    mouseX = (float)x;
                    mouseY = (float)y;
                } else if (action == KeyAction.Release) {
                    window.CursorMode = CursorMode.Normal;
                    looking = false;
                }
            }
        }

        public void Update(double elapsed) {
            UpdatePos(elapsed);
            UpdateRot(elapsed);
        }

        void UpdatePos(double elapsed) {
            float x = 0;
            float y = 0;
            float z = 0;

            if (forward) z += 1;
            if (back) z -= 1;
            if (right) x += 1;
            if (left) x -= 1;
            if (up) y += 1;
            if (down) y -= 1;

            if (x != 0 || y != 0 || z != 0) {
                Vector3 dir = new Vector3(x, y, z);
                dir = Vector3.Normalize(dir);
                x = dir.X;
                y = dir.Y;
                z = dir.Z;
            }

            Quaternion rot = camera.Rotation;
            Vector3 pos = camera.Position;

            Vector3 camForward = Vector3.Transform(new Vector3(0, 0, -1), rot);
            Vector3 camRight = Vector3.Transform(new Vector3(1, 0, 0), rot);
            Vector3 camUp = Vector3.Transform(new Vector3(0, 1, 0), rot);

            pos += (float)elapsed * x * camRight;
            pos += (float)elapsed * y * camUp;
            pos += (float)elapsed * z * camForward;

            camera.Position = pos;
        }

        void UpdateRot(double elapsed) {
            float radX = lookX * (float)Math.PI / 180;
            float radY = lookY * (float)Math.PI / 180;

            Quaternion rot = Quaternion.CreateFromYawPitchRoll(-radX, 0, 0) * Quaternion.CreateFromYawPitchRoll(0, -radY, 0);
            camera.Rotation = rot;
        }
    }
}
