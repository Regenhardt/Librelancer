﻿// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Collections.Generic;
    
namespace LibreLancer
{
    public class XmlUIServerList : XmlUIPanel
    {
        const int NUM_ROWS = 8;

        dynamic descriptionPanel;
        string descriptionText;
        GridControl grid;

        float[] dividerPositions =
        {
            0.252f,
            0.46f,
            0.55f,
            0.63f,
            0.73f,
            0.82f,
            0.88f
        };

        string[] columnTitles =
        {
            "NAME",
            "IP",
            "VISITED",
            "PING",
            "PLAYERS",
            "VERSION",
            "LAN",
            "OPTIONS"
        };
        public List<LocalServerInfo> Servers = new List<LocalServerInfo>();

        public XmlUIServerList(XInt.ServerList sl, XInt.Style style, XmlUIScene scene) : base(style,scene,false)
        {
            Positioning = sl;
            ID = sl.ID;
            Lua = new ServerListAPI(this);
            grid = new GridControl(scene, dividerPositions, columnTitles, GetGridRect, new ServerListContent(this), NUM_ROWS);
        }

        public override void OnMouseDown() => grid.OnMouseDown();
        public override void OnMouseUp() => grid.OnMouseUp();

        int _selected = -1;

        public int Selection
        {
            get { return _selected; }
            set { _selected = value; }
        }

        void UpdateSelection()
        {
            if (descriptionPanel != null) descriptionPanel.text(descriptionText).value(Servers[_selected].Description);
        }

        class ServerListContent : IGridContent
        {
            XmlUIServerList serverList;
            public ServerListContent(XmlUIServerList list) => serverList = list;

            public int Count => serverList.Servers.Count;

            public int Selected { get => serverList._selected; set { serverList._selected = value; serverList.UpdateSelection(); } }

            public string GetContentString(int row, int column)
            {
                var srv = serverList.Servers[row];
                switch (column)
                {
                    case 0:
                        return srv.Name;
                    case 1:
                        return AddressString(srv.EndPoint.Address);
                    case 2:
                        return "NO";
                    case 3:
                        if (srv.Ping == -1) return "???";
                        else return srv.Ping.ToString();
                    case 4:
                        return string.Format("{0}/{1}", srv.CurrentPlayers, srv.MaxPlayers);
                    case 5:
                        return srv.DataVersion;
                    case 6:
                        return "YES";
                }
                return null;
            }
        }

        static string AddressString(System.Net.IPAddress addr)
        {
            if (addr.IsIPv4MappedToIPv6)
                return addr.MapToIPv4().ToString();
            else
                return addr.ToString();
        }

        public class ServerListAPI : PanelAPI 
        {
            public XmlUIServerList Srv;
            public ServerListAPI(XmlUIServerList l) : base(l)
            {
                Srv = l;
            }

            public void linkdescription(dynamic d, string id)
            {
                Srv.descriptionPanel = d;
                Srv.descriptionText = id;
            }

            public bool anyselected()
            {
                return Srv._selected != -1;
            }
        }

        Rectangle GetGridRect()
        {
            var pos = CalculatePosition();
            var sz = CalculateSize();
            return new Rectangle((int)pos.X, (int)pos.Y, (int)sz.X, (int)sz.Y);
        }

        protected override void UpdateInternal(TimeSpan delta, bool updateInput)
        {
            base.UpdateInternal(delta, updateInput);
            if(updateInput)
                grid.Update();
        }

        protected override void DrawInternal(TimeSpan delta)
        {
            base.DrawInternal(delta);
            Scene.Renderer2D.Start(Scene.GWidth, Scene.GHeight);
            grid.Draw();
            Scene.Renderer2D.Finish();
        }
    }
}
