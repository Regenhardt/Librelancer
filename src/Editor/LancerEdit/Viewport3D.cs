﻿// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using LibreLancer;
using LibreLancer.ImUI;
using ImGuiNET;

namespace LancerEdit
{
    public class Viewport3D : IDisposable
    {
        RenderState rstate;
        ViewportManager vps;
        int rw = -1, rh = -1;
        int mrw = -1, mrh = -1, msamples = 0;
        int rid;
        public RenderTarget2D RenderTarget;
        MultisampleTarget msaa;
        public Vector3 DefaultOffset = new Vector3(0, 0, 200);

        public float ModelScale = 0.25f;
        public Vector2 Rotation = Vector2.Zero;
        public Vector2 CameraRotation = Vector2.Zero;
        public int MarginH = 40;
        public int MarginW = 15;
        public int MinWidth = 120;
        public int MinHeight = 120;

        public Vector3 CameraOffset = Vector3.Zero;
        public Color4 Background = Color4.CornflowerBlue * new Color4(0.3f, 0.3f, 0.3f, 1f);

        public int RenderWidth { get { return rw; }}
        public int RenderHeight { get { return rh; }}
        
        MainWindow mw;
        public Viewport3D(MainWindow mw) 
        {
            this.mw = mw;
            rstate = mw.RenderState;
            vps = mw.Viewport;
        }

        public void ResetControls()
        {
            CameraOffset = Vector3.Zero;
            Rotation = CameraRotation = Vector2.Zero;
            
        }
        Color4 cc;
        public void Begin(int fixWidth = -1, int fixHeight = -1)
        {
            var renderWidth = Math.Max(120, (int)ImGui.GetWindowWidth() - MarginW);
            var renderHeight = Math.Max(120, (int)ImGui.GetWindowHeight() - MarginH);
            if (fixWidth > 0) renderWidth = fixWidth;
            if (fixHeight > 0) renderHeight = fixHeight;
            //Generate render target
            if (rh != renderHeight || rw != renderWidth)
            {
                if (RenderTarget != null)
                {
                    ImGuiHelper.DeregisterTexture(RenderTarget);
                    RenderTarget.Dispose();
                }
                RenderTarget = new RenderTarget2D(renderWidth, renderHeight);
                rid = ImGuiHelper.RegisterTexture(RenderTarget);
                rw = renderWidth;
                rh = renderHeight;
            }
            if (mw.Config.MSAA != 0 && ((mrw != rw) || (mrh != rh) || (msamples != mw.Config.MSAA)))
            {
                if (msaa != null) msaa.Dispose();
                msaa = new MultisampleTarget(rw, rh, mw.Config.MSAA);

            } else if(msaa != null)
            {
                msaa.Dispose();
                mrw = mrh = -1;
                msamples = 0;
                msaa = null;
            }
            cc = rstate.ClearColor;
            if (mw.Config.MSAA != 0)
                msaa.Bind();
            else
                RenderTarget.BindFramebuffer();
            vps.Push(0, 0, rw, rh);
            rstate.Cull = true;
            rstate.DepthEnabled = true;
            rstate.ClearColor = Background;
            rstate.ClearAll();
        }

        public void End(bool view = true)
        {
            vps.Pop();
            RenderTarget2D.ClearBinding();
            if (mw.Config.MSAA != 0)
                msaa.BlitToRenderTarget(RenderTarget);
            rstate.ClearColor = cc;
            rstate.DepthEnabled = false;
            rstate.BlendMode = BlendMode.Normal;
            rstate.Cull = false;
            //Viewport Control
            if (view)
            {
                ImGui.ImageButton((IntPtr)rid, new Vector2(rw, rh),
                                  new Vector2(0,1), new Vector2(1,0),
                                  0,
                                  Vector4.One, Vector4.One);
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.None))
                {
                    switch(mw.Config.CameraMode)
                    {
                        case CameraModes.Default:
                            DefaultControls();
                            break;
                        case CameraModes.Orbit:
                            OrbitControls();
                            break;
                    }
                }
            }
        }

        float GotoRadius => ModelScale * 5.2f;

        public void GoTop()
        {
            Rotation = new Vector2(0, 0.5f * (float)Math.PI);
            CameraOffset = new Vector3(0, 0, GotoRadius * 1.1f);
        }

        public void GoBottom()
        {
            Rotation = new Vector2(0, -0.5f * (float)Math.PI);
            CameraOffset = new Vector3(0, 0, GotoRadius * 1.1f);
        }

        public void GoLeft()
        {
            CameraRotation = Vector2.Zero;
            CameraOffset = new Vector3(0, 0, GotoRadius);
            Rotation = new Vector2(0.5f * (float)Math.PI, 0);
        }

        public void GoRight()
        {
            CameraRotation = Vector2.Zero;
            CameraOffset = new Vector3(0, 0, GotoRadius);
            Rotation = new Vector2(-0.5f * (float)Math.PI, 0);
        }

        public void GoFront()
        {
            CameraRotation = Vector2.Zero;
            CameraOffset = new Vector3(0, 0, GotoRadius);
            Rotation = new Vector2((float)-Math.PI, 0);
        }

        public void GoBack()
        {
            CameraRotation = Vector2.Zero;
            CameraOffset = new Vector3(0, 0, GotoRadius);
            Rotation = Vector2.Zero;
        }

        void OrbitControls()
        {
            var io = ImGui.GetIO();
            CameraRotation = Vector2.Zero;
            CameraOffset.X = CameraOffset.Y = 0;
            if (ImGui.IsMouseDragging(0, 1f))
            {
                var delta = (Vector2)ImGui.GetMouseDragDelta(0, 1f);
                ImGui.ResetMouseDragDelta(0);
                Rotation += (delta / 100) * new Vector2(1, -1);
            }
            else if (ImGui.IsMouseDragging(1, 1f))
            {
                var delta = (Vector2)ImGui.GetMouseDragDelta(1, 1f);
                ImGui.ResetMouseDragDelta(1);
                var mouseZoomStep = ModelScale / 56f;
                CameraOffset.Z -= delta.Y * mouseZoomStep;
            }
            float wheel = ImGui.GetIO().MouseWheel;
            var zoomStep = ModelScale * 1.05f;
            if (io.KeyShift)
                CameraOffset.Z -= wheel * (2 * zoomStep);
            else
                CameraOffset.Z -= wheel * zoomStep;
            if (CameraOffset.Z < 0) CameraOffset.Z = 0;
        }

        void DefaultControls()
        {
            var io = ImGui.GetIO();
            if (ImGui.IsMouseDragging(0, 1f))
            {
                var delta = (Vector2)ImGui.GetMouseDragDelta(0, 1f);
                ImGui.ResetMouseDragDelta(0);
                var rotmat = Matrix4.CreateRotationX(CameraRotation.Y) *
                    Matrix4.CreateRotationY(CameraRotation.X);
                if (ImGui.IsMouseDown(1))
                {
                    //LMB + RMB - Move up and down
                    ImGui.ResetMouseDragDelta(1);
                    var y = rotmat.Transform(Vector3.UnitY);
                    CameraOffset += y * (delta.Y * ModelScale / 52f);
                }
                else
                {
                    var z = rotmat.Transform(Vector3.UnitZ);
                    var x = rotmat.Transform(Vector3.UnitX);

                    CameraOffset += x * (delta.X * ModelScale / 52f);
                    CameraOffset -= z * (delta.Y * ModelScale / 44f);
                }
            }
            else if (ImGui.IsMouseDragging(1, 1f))
            {
                var delta = (Vector2)ImGui.GetMouseDragDelta(1, 1f);
                ImGui.ResetMouseDragDelta(1);
                if (io.KeyCtrl)
                {
                    //CTRL + RMB - Rotate Model
                    Rotation += (delta / 100) * new Vector2(1, -1);
                }
                else
                {
                    //RMB - Rotate viewport camera
                    CameraRotation -= (delta / 100);
                    DefaultKeyboardControls();
                }
            }
            else if (io.MouseDown[1])
                DefaultKeyboardControls();
        }
        void DefaultKeyboardControls()
        {
            var rotmat = Matrix4.CreateRotationX(CameraRotation.Y) *
                            Matrix4.CreateRotationY(CameraRotation.X);
            if (mw.Keyboard.IsKeyDown(Keys.W))
            {
                var z = rotmat.Transform(-Vector3.UnitZ);
                CameraOffset += z * (float)mw.TimeStep * ModelScale;
            }
            else if (mw.Keyboard.IsKeyDown(Keys.S))
            {
                var z = rotmat.Transform(Vector3.UnitZ);
                CameraOffset += z * (float)mw.TimeStep * ModelScale;
            }
            if (mw.Keyboard.IsKeyDown(Keys.A))
            {
                var x = rotmat.Transform(Vector3.UnitX);
                CameraOffset += x * (float)mw.TimeStep * ModelScale;
            }
            else if (mw.Keyboard.IsKeyDown(Keys.D))
            {
                var x = rotmat.Transform(-Vector3.UnitX);
                CameraOffset += x * (float)mw.TimeStep * ModelScale;
            }
            if (mw.Keyboard.IsKeyDown(Keys.E))
            {
                var y = rotmat.Transform(Vector3.UnitY);
                CameraOffset += y * (float)mw.TimeStep * ModelScale;
            }
            else if (mw.Keyboard.IsKeyDown(Keys.Q))
            {
                var y = rotmat.Transform(-Vector3.UnitY);
                CameraOffset += y * (float)mw.TimeStep * ModelScale;
            }
        }
        public void Dispose()
        {
            if(RenderTarget != null) {
                ImGuiHelper.DeregisterTexture(RenderTarget);
                RenderTarget.Dispose();
            }
        }
    }
}
