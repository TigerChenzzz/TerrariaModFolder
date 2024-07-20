global using Microsoft.Xna.Framework;
global using Microsoft.Xna.Framework.Graphics;
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using Terraria;
global using Terraria.ID;
global using Terraria.Localization;
global using Terraria.ModLoader;
using System.Reflection;
using Terraria.Audio;
using Terraria.GameContent.UI.States;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModFolder;

public class ModFolder : Mod {
    public static ModFolder Instance { get; private set; } = null!;

    public override void Load() {
        Instance = this;
        On_UIWorkshopHub.OnInitialize += On_UIWorkshopHub_OnInitialize;
        var bfs = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MonoModHooks.Add(typeof(Interface).GetMethod(nameof(Interface.ModLoaderMenus), bfs), On_Interface_ModLoaderMenus);
        var configList = Interface.modConfigList;
        if (!configList._isInitialized)
            configList.Initialize();
        Interface.modConfigList.backButton.OnLeftClick += UIModConfigList_BackButton_OnLeftClick;
    }
    public override void Unload() {
        Interface.modConfigList.backButton.OnLeftClick -= UIModConfigList_BackButton_OnLeftClick;
    }

    #region 在 UIModConfigList 中返回时尝试回到文件夹页面
    private void UIModConfigList_BackButton_OnLeftClick(UIMouseEvent evt, UIElement listeningElement) {
        if (Main.gameMenu && UIModFolder.IsPreviousUIStateOfConfigList) {
            UIModFolder.IsPreviousUIStateOfConfigList = false;
            Main.menuMode = UIModFolder.MyMenuMode;
        }
    }
    #endregion
    #region 给予文件夹页面一个 menuMode, 当将 Main.menuMode 设置为它时自动到达文件夹页面
    private delegate void InterfaceModLoaderMenusDelegate(Main main, int selectedMenu, string[] buttonNames, float[] buttonScales, int[] buttonVerticalSpacing, ref int offY, ref int spacing, ref int numButtons, ref bool backButtonDown);
    private static void On_Interface_ModLoaderMenus(InterfaceModLoaderMenusDelegate orig, Main main, int selectedMenu, string[] buttonNames, float[] buttonScales, int[] buttonVerticalSpacing, ref int offY, ref int spacing, ref int numButtons, ref bool backButtonDown) {
        orig(main, selectedMenu, buttonNames, buttonScales, buttonVerticalSpacing, ref offY, ref spacing, ref numButtons, ref backButtonDown);
        if (Main.menuMode == UIModFolder.MyMenuMode) {
            Main.MenuUI.SetState(UIModFolder.Instance);
            Main.menuMode = 888;
        }
    }
    #endregion
    #region 在创意工坊页面添加右键进入的实现与提示
    private void On_UIWorkshopHub_OnInitialize(On_UIWorkshopHub.orig_OnInitialize orig, UIWorkshopHub self) {
        orig(self);
        var buttonMods = self._buttonMods;
        buttonMods.OnMouseOver += (_, _) => {
            self._descriptionText.SetText(string.Join(' ', self._descriptionText.Text, Instance.GetLocalization("UI.HoverTextOnButtonMods").Value));
        };
        buttonMods.OnRightClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            UIModFolder.Instance.PreviousUIState = self;
            Main.MenuUI.SetState(UIModFolder.Instance);
        };
    }
    #endregion
}
