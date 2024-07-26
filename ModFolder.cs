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
using ModFolder.Systems;
using ModFolder.UI;
using System.Reflection;
using Terraria.Audio;
using Terraria.GameContent.UI.States;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModFolder;

// TODO: 在各处确认删除的界面中点击其它部分直接取消 (直接糊上一个 UIState)
// TODO: 批量订阅 / 删除 (删除索引 / 取消订阅 / Both)    本文件夹下 / 本文件夹下及所有子文件夹下 (alt 控制)
// TODO: 关于各处二次确认和 ctrl shift alt 的联动: 二次确认界面有三个提示指示分别有什么用, 按下对应键时对应提示亮起且此时按确认时才会有对应效果
// TODO: Node 需要有父节点
// TODO: 文件夹快捷方式

// TODO: 模组和文件夹的移动 (包括顺序和改变路径)
// TODO: 在自定义排序时右键拖动 (右键拖动时禁用左键?)

// TODO: 排序: 自定义排序, 文件夹在模组上 / 下 / 任意
// TODO: 分组与筛选: 按类型 (客户服务端文件夹), 按 Steam 还是本地文件, 按[是否启用 / 准备启用或禁用 / 修改了启禁用状态], 收藏

// TODO: 文件夹的最近更新属性
// TODO: ModNode 保存显示名? (配置)
// TODO: 启用文件夹 / 禁用文件夹 / 显示文件夹的部分启用(背景为宽斜杠白条)

// TODO: 查看全部模组的界面
// TODO: 查看未安排位置的模组的界面
// TODO: 配置是否将未安排位置的模组自动安排在根目录, 或安排到某个目录
// TODO: 配置是否将未安排位置的模组显示在根目录, 或显示在某个目录
// TODO: 标记未安排位置的模组

// TODO: 美化界面 (那几个按钮太丑了)
// TODO: 块状排列 (ConciseModList: ???)
// TODO: Mouse4 和 Mouse5 撤回与回退
// TODO: alt 收藏模组 (收藏的模组一般不会因禁用全部按钮而被禁用) (金光闪闪和粒子) (启用禁用颜色改用绿红)

public class ModFolder : Mod {
    public static ModFolder Instance { get; private set; } = null!;

    public override void Load() {
        Instance = this;
        FolderDataSystem.Reload();
        On_UIWorkshopHub.OnInitialize += On_UIWorkshopHub_OnInitialize;
        var bfs = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MonoModHooks.Add(typeof(Interface).GetMethod(nameof(Interface.ModLoaderMenus), bfs), On_Interface_ModLoaderMenus);
        var configList = Interface.modConfigList;
        if (!configList._isInitialized) {
            configList.Initialize();
        }
        if (Interface.modConfigList.backButton is { } backButton) {
            backButton.OnLeftClick += UIModConfigList_BackButton_OnLeftClick;
        }
    }
    public override void Unload() {
        FolderDataSystem.Save();
        if (Interface.modConfigList?.backButton is { } backButton) {
            backButton.OnLeftClick -= UIModConfigList_BackButton_OnLeftClick;
        }
    }

    #region 在 UIModConfigList 中返回时尝试回到文件夹页面
    private void UIModConfigList_BackButton_OnLeftClick(UIMouseEvent evt, UIElement listeningElement) {
        if (Main.gameMenu && UIModFolderMenu.IsPreviousUIStateOfConfigList) {
            UIModFolderMenu.IsPreviousUIStateOfConfigList = false;
            Main.menuMode = UIModFolderMenu.MyMenuMode;
        }
    }
    #endregion
    #region 给予文件夹页面一个 menuMode, 当将 Main.menuMode 设置为它时自动到达文件夹页面
    private delegate void InterfaceModLoaderMenusDelegate(Main main, int selectedMenu, string[] buttonNames, float[] buttonScales, int[] buttonVerticalSpacing, ref int offY, ref int spacing, ref int numButtons, ref bool backButtonDown);
    private static void On_Interface_ModLoaderMenus(InterfaceModLoaderMenusDelegate orig, Main main, int selectedMenu, string[] buttonNames, float[] buttonScales, int[] buttonVerticalSpacing, ref int offY, ref int spacing, ref int numButtons, ref bool backButtonDown) {
        orig(main, selectedMenu, buttonNames, buttonScales, buttonVerticalSpacing, ref offY, ref spacing, ref numButtons, ref backButtonDown);
        if (Main.menuMode == UIModFolderMenu.MyMenuMode) {
            Main.MenuUI.SetState(UIModFolderMenu.Instance);
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
            // !!!!! Test
#if DEBUG
            UIModFolderMenu.TotallyReload();
            FolderDataSystem.Reload();
#endif
            UIModFolderMenu.Instance.PreviousUIState = self;
            Main.MenuUI.SetState(UIModFolderMenu.Instance);
        };
    }
    #endregion
}
