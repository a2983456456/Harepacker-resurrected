﻿using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.MapObjects.UIObject;
using HaCreator.MapSimulator.Objects;
using HaCreator.MapSimulator.Objects.FieldObject;
using HaCreator.MapSimulator.Objects.UIObject;
using HaCreator.Wz;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HaSharedLibrary.Wz;
using MapleLib.Helpers;
using SharpDX.Direct2D1.Effects;

namespace HaCreator.MapSimulator
{
    public class MapSimulatorLoader
    {
        private const string GLOBAL_FONT = "Arial";

        /// <summary>
        /// Create map simulator board
        /// </summary>
        /// <param name="mapBoard"></param>
        /// <param name="titleName"></param>
        /// <returns></returns>
        public static MapSimulator CreateAndShowMapSimulator(Board mapBoard, string titleName)
        {
            if (mapBoard.MiniMap == null)
                mapBoard.RegenerateMinimap();

            MapSimulator mapSimulator = null;

            Thread thread = new Thread(() =>
            {
                mapSimulator = new MapSimulator(mapBoard, titleName);
                mapSimulator.Run();
            })
            {
                Priority = ThreadPriority.Highest
            };
            thread.Start();
            thread.Join();

            return mapSimulator;
        }

        #region Common
        /// <summary>
        /// Load frames from WzSubProperty or WzCanvasProperty
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="spineAni">Spine animation path</param>
        /// <returns></returns>
        private static List<IDXObject> LoadFrames(TexturePool texturePool, WzImageProperty source, int x, int y, GraphicsDevice device, ref List<WzObject> usedProps, string spineAni = null)
        {
            List<IDXObject> frames = new List<IDXObject>();

            source = WzInfoTools.GetRealProperty(source);

            if (source is WzSubProperty property1 && property1.WzProperties.Count == 1)
            {
                source = property1.WzProperties[0];
            }

            if (source is WzCanvasProperty property) //one-frame
            {
                bool bLoadedSpine = LoadSpineMapObjectItem(source, source, device, spineAni);
                if (!bLoadedSpine)
                {
                    string canvasBitmapPath = property.FullPath;
                    Texture2D textureFromCache = texturePool.GetTexture(canvasBitmapPath);
                    if (textureFromCache != null)
                    {
                        source.MSTag = textureFromCache;
                    }
                    else
                    {
                        source.MSTag = property.GetLinkedWzCanvasBitmap().ToTexture2D(device);

                        // add to cache
                        texturePool.AddTextureToPool(canvasBitmapPath, (Texture2D)source.MSTag);
                    }
                }
                usedProps.Add(source);

                if (source.MSTagSpine != null)
                {
                    WzSpineObject spineObject = (WzSpineObject)source.MSTagSpine;
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXSpineObject(spineObject, x, y, origin));
                }
                else if (source.MSTag != null)
                {
                    Texture2D texture = (Texture2D)source.MSTag;
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture));
                }
                else // fallback
                {
                    Texture2D texture = Properties.Resources.placeholder.ToTexture2D(device);
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture));
                }
            }
            else if (source is WzSubProperty) // animated
            {
                WzImageProperty _frameProp;
                int i = 0;

                while ((_frameProp = WzInfoTools.GetRealProperty(source[(i++).ToString()])) != null)
                {
                    if (_frameProp is WzSubProperty) // issue with 867119250
                    {
                       frames.AddRange( LoadFrames(texturePool, _frameProp, x, y, device, ref usedProps, null));
                    }
                    else
                    {
                        WzCanvasProperty frameProp;

                        if (_frameProp is WzUOLProperty) // some could be UOL. Ex: 321100000 Mirror world: [Mirror World] Leafre
                        {
                            WzObject linkVal = ((WzUOLProperty)_frameProp).LinkValue;
                            if (linkVal is WzCanvasProperty linkCanvas)
                            {
                                frameProp = linkCanvas;
                            }
                            else
                                continue;
                        } else
                        {
                            frameProp = (WzCanvasProperty)_frameProp;
                        }

                        int delay = (int)InfoTool.GetOptionalInt(frameProp["delay"], 100);

                        bool bLoadedSpine = LoadSpineMapObjectItem((WzImageProperty)frameProp.Parent, frameProp, device, spineAni);
                        if (!bLoadedSpine)
                        {
                            if (frameProp.MSTag == null)
                            {
                                string canvasBitmapPath = frameProp.FullPath;
                                Texture2D textureFromCache = texturePool.GetTexture(canvasBitmapPath);
                                if (textureFromCache != null)
                                {
                                    frameProp.MSTag = textureFromCache;
                                }
                                else
                                {
                                    frameProp.MSTag = frameProp.GetLinkedWzCanvasBitmap().ToTexture2D(device);

                                    // add to cache
                                    texturePool.AddTextureToPool(canvasBitmapPath, (Texture2D)frameProp.MSTag);
                                }
                            }
                        }
                        usedProps.Add(frameProp);

                        if (frameProp.MSTagSpine != null)
                        {
                            WzSpineObject spineObject = (WzSpineObject)frameProp.MSTagSpine;
                            System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                            frames.Add(new DXSpineObject(spineObject, x, y, origin, delay));
                        }
                        else if (frameProp.MSTag != null)
                        {
                            Texture2D texture = (Texture2D)frameProp.MSTag;
                            System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                            frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture, delay));
                        }
                        else
                        {
                            Texture2D texture = Properties.Resources.placeholder.ToTexture2D(device);
                            System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                            frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture, delay));
                        }
                    }
                }
            }
            return frames;
        }
        #endregion

        /// <summary>
        /// Map item
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static BaseDXDrawableItem CreateMapItemFromProperty(TexturePool texturePool, WzImageProperty source, int x, int y, Point mapCenter, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
        {
            BaseDXDrawableItem mapItem = new BaseDXDrawableItem(LoadFrames(texturePool, source, x, y, device, ref usedProps), flip);
            return mapItem;
        }

        /// <summary>
        /// Background
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="bgInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static BackgroundItem CreateBackgroundFromProperty(TexturePool texturePool, WzImageProperty source, BackgroundInstance bgInstance, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
        {
            List<IDXObject> frames = LoadFrames(texturePool, source, bgInstance.BaseX, bgInstance.BaseY, device, ref usedProps, bgInstance.SpineAni);
            if (frames.Count == 0)
            {
                string error = string.Format("[MapSimulatorLoader] 0 frames loaded for bg texture from src: '{0}'", source.FullPath); // Back_003.wz\\BM3_3.img\\spine\\0

                ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                return null;
            }
            
            if (frames.Count == 1)
            {
                return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, frames[0], flip, bgInstance.screenMode);
            }
            return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, frames, flip, bgInstance.screenMode);
        }

        #region Spine
        /// <summary>
        /// Load spine object from WzImageProperty (bg, map item)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="prop"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        private static bool LoadSpineMapObjectItem(WzImageProperty source, WzImageProperty prop, GraphicsDevice device, string spineAniPath = null)
        {
            WzImageProperty spineAtlas = null;

            bool bIsObjectLayer = source.Parent.Name == "spine";
            if (bIsObjectLayer) // load spine if the source is already the directory we need
            {
                string spineAtlasPath = ((WzStringProperty)source["spine"])?.GetString();
                if (spineAtlasPath != null)
                {
                    spineAtlas = source[spineAtlasPath + ".atlas"];
                }
            }
            else if (spineAniPath != null)
            {
                WzImageProperty spineSource = (WzImageProperty)source.Parent?.Parent["spine"]?[source.Name];

                string spineAtlasPath = ((WzStringProperty)spineSource["spine"])?.GetString();
                if (spineAtlasPath != null)
                {
                    spineAtlas = spineSource[spineAtlasPath + ".atlas"];
                }
            } else // simply check if 'spine' WzStringProperty exist, fix for Adele town
            {
                string spineAtlasPath = ((WzStringProperty)source["spine"])?.GetString();
                if (spineAtlasPath != null)
                {
                    spineAtlas = source[spineAtlasPath + ".atlas"];
                    bIsObjectLayer = true;
                }
            }

            if (spineAtlas != null)
            {
                if (spineAtlas is WzStringProperty stringObj)
                {
                    if (!stringObj.IsSpineAtlasResources)
                        return false;

                    WzSpineObject spineObject = new WzSpineObject(new WzSpineAnimationItem(stringObj));

                    spineObject.spineAnimationItem.LoadResources(device); //  load spine resources (this must happen after window is loaded)
                    spineObject.skeleton = new Skeleton(spineObject.spineAnimationItem.SkeletonData);
                    //spineObject.skeleton.R =153;
                    //spineObject.skeleton.G = 255;
                    //spineObject.skeleton.B = 0;
                    //spineObject.skeleton.A = 1f;

                    // Skin
                    foreach (Skin skin in spineObject.spineAnimationItem.SkeletonData.Skins)
                    {
                        spineObject.skeleton.SetSkin(skin); // just set the first skin
                        break;
                    }

                    // Define mixing between animations.
                    spineObject.stateData = new AnimationStateData(spineObject.skeleton.Data);
                    spineObject.state = new AnimationState(spineObject.stateData);
                    if (!bIsObjectLayer)
                        spineObject.state.TimeScale = 0.1f;

                    if (spineAniPath != null)
                    {
                        spineObject.state.SetAnimation(0, spineAniPath, true);
                    }
                    else
                    {
                        int i = 0;
                        foreach (Animation animation in spineObject.spineAnimationItem.SkeletonData.Animations)
                        {
                            spineObject.state.SetAnimation(i++, animation.Name, true);
                        }
                    }
                    prop.MSTagSpine = spineObject;
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Reactor
        /// <summary>
        /// Create reactor item
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="reactorInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static ReactorItem CreateReactorFromProperty(TexturePool texturePool, ReactorInstance reactorInstance, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            ReactorInfo reactorInfo = (ReactorInfo)reactorInstance.BaseInfo;
            
            List<IDXObject> frames = new List<IDXObject>();

            WzImage linkedReactorImage = reactorInfo.LinkedWzImage;
            if (linkedReactorImage != null)
            {
                WzImageProperty framesImage = (WzImageProperty)linkedReactorImage?["0"]?["0"];
                if (framesImage != null)
                {
                    frames = LoadFrames(texturePool, framesImage, reactorInstance.X, reactorInstance.Y, device, ref usedProps);
                }
            }
            if (frames.Count == 0)
            {
                //string error = string.Format("[MapSimulatorLoader] 0 frames loaded for reactor from src: '{0}'",  reactorInfo.ID);

                //ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                return null;
            }
            return new ReactorItem(reactorInstance, frames);
        }
        #endregion

        #region Portal       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="gameParent"></param>
        /// <param name="portalInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static PortalItem CreatePortalFromProperty(TexturePool texturePool, WzSubProperty gameParent, PortalInstance portalInstance, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            PortalInfo portalInfo = (PortalInfo)portalInstance.BaseInfo;

            if (portalInstance.pt == PortalType.PORTALTYPE_STARTPOINT ||
                portalInstance.pt == PortalType.PORTALTYPE_INVISIBLE ||
                //portalInstance.pt == PortalType.PORTALTYPE_CHANGABLE_INVISIBLE ||
                portalInstance.pt == PortalType.PORTALTYPE_SCRIPT_INVISIBLE ||
                portalInstance.pt == PortalType.PORTALTYPE_SCRIPT ||
                portalInstance.pt == PortalType.PORTALTYPE_COLLISION ||
                portalInstance.pt == PortalType.PORTALTYPE_COLLISION_SCRIPT ||
                portalInstance.pt == PortalType.PORTALTYPE_COLLISION_CUSTOM_IMPACT || // springs in Mechanical grave 350040240
                portalInstance.pt == PortalType.PORTALTYPE_COLLISION_VERTICAL_JUMP) // vertical spring actually
                return null;

            List<IDXObject> frames = new List<IDXObject>(); // All frames "stand", "speak" "blink" "hair", "angry", "wink" etc

            //string portalType = portalInstance.pt;
            //int portalId = Program.InfoManager.PortalIdByType[portalInstance.pt];

            WzSubProperty portalTypeProperty = (WzSubProperty)gameParent[portalInstance.pt];
            if (portalTypeProperty == null)
            {
                portalTypeProperty = (WzSubProperty)gameParent["pv"];
            } 
            else
            {
                // Support for older versions of MapleStory where 'pv' is a subproperty for the image frame than a collection of subproperty of frames
                if (portalTypeProperty["0"] is WzCanvasProperty)
                {
                    frames.AddRange(LoadFrames(texturePool, portalTypeProperty, portalInstance.X, portalInstance.Y, device, ref usedProps));
                    portalTypeProperty = null;
                }
            }

            if (portalTypeProperty != null)
            {
                WzSubProperty portalImageProperty = (WzSubProperty)portalTypeProperty[portalInstance.image == null ? "default" : portalInstance.image];

                if (portalImageProperty != null)
                {
                    WzSubProperty framesPropertyParent;
                    if (portalImageProperty["portalContinue"] != null)
                        framesPropertyParent = (WzSubProperty)portalImageProperty["portalContinue"];
                    else
                        framesPropertyParent = (WzSubProperty)portalImageProperty;

                    if (framesPropertyParent != null)
                    {
                        frames.AddRange(LoadFrames(texturePool, framesPropertyParent, portalInstance.X, portalInstance.Y, device, ref usedProps));
                    }
                }
            }
            if (frames.Count == 0)
                return null;
            return new PortalItem(portalInstance, frames);
        }
        #endregion

        #region Life
        /// <summary>
        /// 
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="mobInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static MobItem CreateMobFromProperty(TexturePool texturePool, MobInstance mobInstance, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            MobInfo mobInfo = (MobInfo)mobInstance.BaseInfo;
            WzImage source = mobInfo.LinkedWzImage;

            List <IDXObject> frames = new List<IDXObject>(); // All frames "stand", "speak" "blink" "hair", "angry", "wink" etc

            foreach (WzImageProperty childProperty in source.WzProperties)
            {
                if (childProperty is WzSubProperty mobStateProperty) // issue with 867119250, Eluna map mobs
                {
                    switch (mobStateProperty.Name)
                    {
                        case "info": // info/speak/0 WzStringProperty
                            {
                                break;
                            }
                        default:
                            {
                                frames.AddRange(LoadFrames(texturePool, mobStateProperty, mobInstance.X, mobInstance.Y, device, ref usedProps));
                                break;
                            }
                    }
                }
            }
            return new MobItem(mobInstance, frames);
        }

        /// <summary>
        /// NPC
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="npcInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static NpcItem CreateNpcFromProperty(TexturePool texturePool, NpcInstance npcInstance, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            NpcInfo npcInfo = (NpcInfo)npcInstance.BaseInfo;
            WzImage source = npcInfo.LinkedWzImage;

            List<IDXObject> frames = new List<IDXObject>(); // All frames "stand", "speak" "blink" "hair", "angry", "wink" etc

            foreach (WzImageProperty childProperty in source.WzProperties)
            {
                WzSubProperty npcStateProperty = (WzSubProperty)childProperty;
                switch (npcStateProperty.Name)
                {
                    case "info": // info/speak/0 WzStringProperty
                        {
                            break;
                        }
                    default:
                        {
                            frames.AddRange(LoadFrames(texturePool, npcStateProperty, npcInstance.X, npcInstance.Y, device, ref usedProps));
                            break;
                        }
                }
            }
            return new NpcItem(npcInstance, frames);
        }
        #endregion

        #region UI
        /// <summary>
        /// Draws the frame and the UI of the minimap.
        /// TODO: This whole thing needs to be dramatically simplified via further abstraction to keep it noob-proof :(
        /// </summary>
        /// <param name="uiWindow1Image">UI.wz/UIWindow1.img pre-bb</param>
        /// <param name="uiWindow2Image">UI.wz/UIWindow2.img post-bb</param>
        /// <param name="uiBasicImage">UI.wz/Basic.img</param>
        /// <param name="mapBoard"></param>
        /// <param name="device"></param>
        /// <param name="UserScreenScaleFactor">The scale factor of the window (DPI)</param>
        /// <param name="MapName">The map name. i.e The Hill North</param>
        /// <param name="StreetName">The street name. i.e Hidden street</param>
        /// <param name="soundUIImage">Sound.wz/UI.img</param>
        /// <param name="bBigBang">Big bang update</param>
        /// <returns></returns>
        public static MinimapItem CreateMinimapFromProperty(WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage uiBasicImage, Board mapBoard, GraphicsDevice device, float UserScreenScaleFactor, string MapName, string StreetName, WzImage soundUIImage, bool bBigBang)
        {
            if (mapBoard.MiniMap == null)
                return null;

            WzSubProperty minimapFrameProperty = (WzSubProperty)uiWindow2Image?["MiniMap"];
            if (minimapFrameProperty == null) // UIWindow2 not available pre-BB.
            {
                minimapFrameProperty = (WzSubProperty)uiWindow1Image["MiniMap"];
            }

            WzSubProperty maxMapProperty = (WzSubProperty)minimapFrameProperty["MaxMap"];
            WzSubProperty miniMapProperty = (WzSubProperty)minimapFrameProperty["MinMap"];
            WzSubProperty maxMapMirrorProperty = (WzSubProperty)minimapFrameProperty["MaxMapMirror"]; // for Zero maps
            WzSubProperty miniMapMirrorProperty = (WzSubProperty)minimapFrameProperty["MinMapMirror"]; // for Zero maps


            WzSubProperty useFrame;
            if (mapBoard.MapInfo.zeroSideOnly || MapConstants.IsZerosTemple(mapBoard.MapInfo.id)) // zero's temple
                useFrame = maxMapMirrorProperty;
            else useFrame = maxMapProperty;

            // Wz frames
            System.Drawing.Bitmap c = ((WzCanvasProperty)useFrame?["c"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap e = ((WzCanvasProperty)useFrame?["e"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap n = ((WzCanvasProperty)useFrame?["n"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap s = ((WzCanvasProperty)useFrame?["s"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap w = ((WzCanvasProperty)useFrame?["w"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap ne = ((WzCanvasProperty)useFrame?["ne"])?.GetLinkedWzCanvasBitmap(); // top right
            System.Drawing.Bitmap nw = ((WzCanvasProperty)useFrame?["nw"])?.GetLinkedWzCanvasBitmap(); // top left
            System.Drawing.Bitmap se = ((WzCanvasProperty)useFrame?["se"])?.GetLinkedWzCanvasBitmap(); // bottom right
            System.Drawing.Bitmap sw = ((WzCanvasProperty)useFrame?["sw"])?.GetLinkedWzCanvasBitmap(); // bottom left

            // Constants
            const float TOOLTIP_FONTSIZE = 10f;
            const int MAPMARK_IMAGE_ALIGN_LEFT = 7; // the number of pixels from the left to draw the map mark image
            const int MAP_IMAGE_PADDING = 2; // the number of pixels from the left to draw the minimap image
            System.Drawing.Color color_bgFill = System.Drawing.Color.Transparent;
            System.Drawing.Color color_foreGround = System.Drawing.Color.White;

            string renderText = string.Format("{0}{1}{2}", StreetName, Environment.NewLine, MapName);


            // Map background image
            System.Drawing.Bitmap miniMapImage = mapBoard.MiniMap; // the original minimap image without UI frame overlay
            int effective_width = miniMapImage.Width + e.Width + w.Width;
            int effective_height = miniMapImage.Height + n.Height + s.Height;

            using (System.Drawing.Font font = new System.Drawing.Font(GLOBAL_FONT, TOOLTIP_FONTSIZE / UserScreenScaleFactor))
            {
                // Get the width of the 'streetName' or 'mapName'
                System.Drawing.Graphics graphics_dummy = System.Drawing.Graphics.FromImage(new System.Drawing.Bitmap(1, 1)); // dummy image just to get the Graphics object for measuring string
                System.Drawing.SizeF tooltipSize = graphics_dummy.MeasureString(renderText, font);

                effective_width = Math.Max((int)tooltipSize.Width + nw.Width, effective_width); // set new width

                int miniMapAlignXFromLeft = MAP_IMAGE_PADDING;
                if (effective_width > miniMapImage.Width) // if minimap is smaller in size than the (text + frame), minimap will be aligned to the center instead
                    miniMapAlignXFromLeft = (effective_width - miniMapImage.Width) / 2/* - miniMapAlignXFromLeft*/;

                System.Drawing.Bitmap miniMapUIImage = new System.Drawing.Bitmap(effective_width, effective_height);
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(miniMapUIImage))
                {
                    // Frames and background
                    UIFrameHelper.DrawUIFrame(graphics, color_bgFill, ne, nw, se, sw, e, w, n, s, null, effective_width, effective_height);

                    // Map name + street name
                    graphics.DrawString(
                        renderText,
                        font, new System.Drawing.SolidBrush(color_foreGround), 50, 20);

                    // Map mark
                    if (Program.InfoManager.MapMarks.ContainsKey(mapBoard.MapInfo.mapMark))
                    {
                        System.Drawing.Bitmap mapMark = Program.InfoManager.MapMarks[mapBoard.MapInfo.mapMark];
                        graphics.DrawImage(mapMark.ToImage(), MAPMARK_IMAGE_ALIGN_LEFT, 17);
                    }

                    // Map image
                    graphics.DrawImage(miniMapImage,
                        miniMapAlignXFromLeft, // map is on the center
                        n.Height);

                    graphics.Flush();
                }

                // Dots pixel 
                System.Drawing.Bitmap bmp_DotPixel = new System.Drawing.Bitmap(2, 4);
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_DotPixel))
                {
                    graphics.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.Yellow), new System.Drawing.RectangleF(0, 0, bmp_DotPixel.Width, bmp_DotPixel.Height));
                    graphics.Flush();
                }
                IDXObject dxObj_miniMapPixel = new DXObject(0, n.Height, bmp_DotPixel.ToTexture2D(device), 0);
                BaseDXDrawableItem item_pixelDot = new BaseDXDrawableItem(dxObj_miniMapPixel, false)
                {
                    Position = new Point(
                    miniMapAlignXFromLeft, // map is on the center
                    0)
                };

                // Map
                Texture2D texturer_miniMap = miniMapUIImage.ToTexture2D(device);

                IDXObject dxObj = new DXObject(0, 0, texturer_miniMap, 0);
                MinimapItem minimapItem = new MinimapItem(dxObj, item_pixelDot);

                ////////////// Minimap buttons////////////////////
                // This must be in order. 
                // >>> If aligning from the left to the right. Items at the left must be at the top of the code
                // >>> If aligning from the right to the left. Items at the right must be at the top of the code with its (x position - parent width).
                // TODO: probably a wrapper class in the future, such as HorizontalAlignment and VerticalAlignment, or Grid/ StackPanel 
                WzBinaryProperty BtMouseClickSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseClick"];
                WzBinaryProperty BtMouseOverSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseOver"];

                if (bBigBang)
                {
                    WzSubProperty BtNpc = (WzSubProperty)minimapFrameProperty["BtNpc"]; // npc button
                    WzSubProperty BtMin = (WzSubProperty)minimapFrameProperty["BtMin"]; // mininise button
                    WzSubProperty BtMax = (WzSubProperty)minimapFrameProperty["BtMax"]; // maximise button
                    WzSubProperty BtBig = (WzSubProperty)minimapFrameProperty["BtBig"]; // big button
                    WzSubProperty BtMap = (WzSubProperty)minimapFrameProperty["BtMap"]; // world button

                    UIObject objUIBtMap = new UIObject(BtMap, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_PADDING, MAP_IMAGE_PADDING), device);
                    objUIBtMap.X = effective_width - objUIBtMap.CanvasSnapshotWidth - 8; // render at the (width of minimap - obj width)

                    UIObject objUIBtBig = new UIObject(BtBig, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_PADDING, MAP_IMAGE_PADDING), device);
                    objUIBtBig.X = objUIBtMap.X - objUIBtBig.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                    UIObject objUIBtMax = new UIObject(BtMax, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_PADDING, MAP_IMAGE_PADDING), device);
                    objUIBtMax.X = objUIBtBig.X - objUIBtMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                    UIObject objUIBtMin = new UIObject(BtMin, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_PADDING, MAP_IMAGE_PADDING), device);
                    objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                    // BaseClickableUIObject objUINpc = new BaseClickableUIObject(BtNpc, false, new Point(objUIBtMap.CanvasSnapshotWidth + objUIBtBig.CanvasSnapshotWidth + objUIBtMax.CanvasSnapshotWidth + objUIBtMin.CanvasSnapshotWidth, MAP_IMAGE_PADDING), device);

                    minimapItem.InitializeMinimapButtons(objUIBtMin, objUIBtMax, objUIBtBig, objUIBtMap);
                } 
                else
                {
                    WzSubProperty BtMin = (WzSubProperty)uiBasicImage["BtMin"]; // mininise button
                    WzSubProperty BtMax = (WzSubProperty)uiBasicImage["BtMax"]; // maximise button
                    WzSubProperty BtMap = (WzSubProperty)minimapFrameProperty["BtMap"]; // world button

                    UIObject objUIBtMap = new UIObject(BtMap, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_PADDING, MAP_IMAGE_PADDING), device);
                    objUIBtMap.X = effective_width - objUIBtMap.CanvasSnapshotWidth - 8; // render at the (width of minimap - obj width)

                    UIObject objUIBtMax = new UIObject(BtMax, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_PADDING, MAP_IMAGE_PADDING), device);
                    objUIBtMax.X = objUIBtMap.X - objUIBtMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                    UIObject objUIBtMin = new UIObject(BtMin, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_PADDING, MAP_IMAGE_PADDING), device);
                    objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                    // BaseClickableUIObject objUINpc = new BaseClickableUIObject(BtNpc, false, new Point(objUIBtMap.CanvasSnapshotWidth + objUIBtBig.CanvasSnapshotWidth + objUIBtMax.CanvasSnapshotWidth + objUIBtMin.CanvasSnapshotWidth, MAP_IMAGE_PADDING), device);

                    minimapItem.InitializeMinimapButtons(objUIBtMin, objUIBtMax, null, objUIBtMap);
                }

                //////////////////////////////////////////////////

                return minimapItem;
            }
        }

        /// <summary>
        /// Tooltip
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="UserScreenScaleFactor">The scale factor of the window (DPI)</param>
        /// <param name="farmFrameParent"></param>
        /// <param name="tooltip"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        public static TooltipItem CreateTooltipFromProperty(TexturePool texturePool, float UserScreenScaleFactor, WzSubProperty farmFrameParent, ToolTipInstance tooltip, GraphicsDevice device)
        {
            // Wz frames
            System.Drawing.Bitmap c = ((WzCanvasProperty)farmFrameParent?["c"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap cover = ((WzCanvasProperty)farmFrameParent?["cover"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap e = ((WzCanvasProperty)farmFrameParent?["e"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap n = ((WzCanvasProperty)farmFrameParent?["n"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap s = ((WzCanvasProperty)farmFrameParent?["s"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap w = ((WzCanvasProperty)farmFrameParent?["w"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap ne = ((WzCanvasProperty)farmFrameParent?["ne"])?.GetLinkedWzCanvasBitmap(); // top right
            System.Drawing.Bitmap nw = ((WzCanvasProperty)farmFrameParent?["nw"])?.GetLinkedWzCanvasBitmap(); // top left
            System.Drawing.Bitmap se = ((WzCanvasProperty)farmFrameParent?["se"])?.GetLinkedWzCanvasBitmap(); // bottom right
            System.Drawing.Bitmap sw = ((WzCanvasProperty)farmFrameParent?["sw"])?.GetLinkedWzCanvasBitmap(); // bottom left


            // tooltip property
            string title = tooltip.Title;
            string desc = tooltip.Desc;

            string renderText = string.Format("{0}{1}{2}", title, Environment.NewLine, desc);

            // Constants
            const float TOOLTIP_FONTSIZE = 9.25f; // thankie willified, ya'll be remembered forever here <3
            //System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(230, 17, 54, 82); // pre V patch (dark blue theme used post-bb), leave this here in case someone needs it
            System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(255,17, 17, 17); // post V patch (dark black theme used), use color picker on paint via image extracted from WZ if you need to get it
            System.Drawing.Color color_foreGround = System.Drawing.Color.White;
            const int WIDTH_PADDING = 10;
            const int HEIGHT_PADDING = 6;

            // Create
            using (System.Drawing.Font font = new System.Drawing.Font(GLOBAL_FONT, TOOLTIP_FONTSIZE / UserScreenScaleFactor))
            {
                System.Drawing.Graphics graphics_dummy = System.Drawing.Graphics.FromImage(new System.Drawing.Bitmap(1, 1)); // dummy image just to get the Graphics object for measuring string
                System.Drawing.SizeF tooltipSize = graphics_dummy.MeasureString(renderText, font);

                int effective_width = (int)tooltipSize.Width + WIDTH_PADDING;
                int effective_height = (int)tooltipSize.Height + HEIGHT_PADDING;

                System.Drawing.Bitmap bmp_tooltip = new System.Drawing.Bitmap(effective_width, effective_height);
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_tooltip))
                {
                    // Frames and background
                    UIFrameHelper.DrawUIFrame(graphics, color_bgFill, ne, nw, se, sw, e, w, n, s, c, effective_width, effective_height);

                    // Text
                    graphics.DrawString(renderText, font, new System.Drawing.SolidBrush(color_foreGround), WIDTH_PADDING / 2, HEIGHT_PADDING / 2);
                    graphics.Flush();
                }
                IDXObject dxObj = new DXObject(tooltip.X, tooltip.Y, bmp_tooltip.ToTexture2D(device), 0);
                TooltipItem item = new TooltipItem(tooltip, dxObj);
                
                return item;
            }
        }


        /// <summary>
        /// Map item
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static MouseCursorItem CreateMouseCursorFromProperty(TexturePool texturePool, WzImageProperty source, int x, int y, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
        {
            WzSubProperty cursorCanvas = (WzSubProperty)source?["0"];
            WzSubProperty cursorPressedCanvas = (WzSubProperty)source?["1"]; // click

            List<IDXObject> frames = LoadFrames(texturePool, cursorCanvas, x, y, device, ref usedProps);

            BaseDXDrawableItem clickedState = CreateMapItemFromProperty(texturePool, cursorPressedCanvas, 0, 0, new Point(0, 0), device, ref usedProps, false);
            return new MouseCursorItem(frames, clickedState);
        }
        #endregion

        private static string DumpFhList(List<FootholdLine> fhs)
        {
            string res = "";
            foreach (FootholdLine fh in fhs)
                res += fh.FirstDot.X + "," + fh.FirstDot.Y + " : " + fh.SecondDot.X + "," + fh.SecondDot.Y + "\r\n";
            return res;
        }
    }
}
