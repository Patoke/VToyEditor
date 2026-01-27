using Silk.NET.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace VToyEditor
{
    public class Camera
    {
        private static Vector2 _lastMouse;
        
        public static Vector3 camPos = new Vector3(0, 10, 20);
        public static Vector3 camFront = new Vector3(0, 0, -1);
        public static Vector3 camUp = Vector3.UnitY;
        public static float camYaw = -90f;
        public static float camPitch = 0f;
        public static float camZoom = 75f;

        public static Matrix4x4 GetViewMatrix()
        {
            return Matrix4x4.CreateLookAt(camPos, camPos + camFront, camUp);
        }

        public static Matrix4x4 GetProjectionMatrix(float aspectRatio)
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(camZoom * (float)Math.PI / 180f, aspectRatio, 25.0f, 100000.0f);
        }

        public static void Look(IMouse mouse, Vector2 position)
        {
            var lookSensitivity = 0.1f;
            if (_lastMouse == default) { _lastMouse = position; }
            else
            {
                var xOffset = (position.X - _lastMouse.X) * lookSensitivity;
                var yOffset = (position.Y - _lastMouse.Y) * lookSensitivity;
                _lastMouse = position;

                camYaw += xOffset;
                camPitch -= yOffset;

                //We don't want to be able to look behind us by going over our head or under our feet so make sure it stays within these bounds
                camPitch = Math.Clamp(camPitch, -89.0f, 89.0f);

                camFront.X = MathF.Cos(camYaw * (float)Math.PI / 180f) * MathF.Cos(camPitch * (float)Math.PI / 180f);
                camFront.Y = MathF.Sin(camPitch * (float)Math.PI / 180f);
                camFront.Z = MathF.Sin(camYaw * (float)Math.PI / 180f) * MathF.Cos(camPitch * (float)Math.PI / 180f);
                camFront = Vector3.Normalize(camFront);
            }
        }

        public static void Move(IKeyboard keyboard, float dt)
        {
            var moveSpeed = 1000f * dt;

            if (keyboard.IsKeyPressed(Key.ShiftLeft))
            {
                moveSpeed *= 5f;
            }

            // up, down
            if (keyboard.IsKeyPressed(Key.Space))
            {
                camPos.Y += moveSpeed;
            }
            if (keyboard.IsKeyPressed(Key.ControlLeft))
            {
                camPos.Y -= moveSpeed;
            }

            // front, back
            if (keyboard.IsKeyPressed(Key.W))
            {
                camPos += moveSpeed * camFront;
            }
            if (keyboard.IsKeyPressed(Key.S))
            {
                camPos -= moveSpeed * camFront;
            }

            // left, right
            if (keyboard.IsKeyPressed(Key.A))
            {
                camPos -= Vector3.Normalize(Vector3.Cross(camFront, camUp)) * moveSpeed;
            }
            if (keyboard.IsKeyPressed(Key.D))
            {
                camPos += Vector3.Normalize(Vector3.Cross(camFront, camUp)) * moveSpeed;
            }
        }
    }
}
