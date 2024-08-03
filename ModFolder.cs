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
using ModFolder.Configs;
using ModFolder.Systems;
using ModFolder.UI;
using System.Reflection;
using Terraria.Audio;
using Terraria.GameContent.UI.States;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModFolder;

// DOING: 删除文件夹时选择是否让文件回到上一级目录

// TODO: 禁用模组时按住 Shift 禁用其不被其它模组依赖的引用
// TODO: 配置删除文件夹是否必须二次确认
// TODO: 配置在拖动时是否移动列表
// TODO: 显示前置时的 "缺少" 字样显示条件?
// TODO: 文件夹的名字和模组的名字没有对齐
// TODO: 删除索引时同时删除收藏, publishId等信息 (根据引用计数)
// TODO: 筛选最近更新与新添加
// TODO: Ctrl 复制的提示
// TODO: 复制文件夹
// TODO: 文件夹快捷方式
// TODO: 添加 Parent 以及规范化节点创建与修改
// TODO: 批量 重新订阅 / 删除 (删除索引 / 取消订阅 / Both)    本文件夹下 / 本文件夹下及所有子文件夹下 (alt 控制)
// TODO: 关于各处二次确认和 ctrl shift alt 的联动: 二次确认界面有三个提示指示分别有什么用, 按下对应键时对应提示亮起且此时按确认时才会有对应效果
// TODO: Node 需要有父节点
// TODO: 大小模组图标的配置
// TODO: 新建文件夹排序问题
// TODO: description 的本地化支持?
// TODO: 模组的内部名如何查看?
// TODO: 模组名称备注
// TODO: 选中, 选中多个

// TODO: 右键拖动时禁用左键?

// TODO: 添加按钮是否筛选文件夹
// TODO: 分组与筛选: 按类型 (客户端 / 服务端 / 文件夹), 按 Steam 还是本地文件, 按收藏, 筛选最近更新与新添加

// TODO: 文件夹的最近更新属性
// TODO: ModNode 保存显示名? (配置)
// TODO: 启用文件夹 / 禁用文件夹 / 显示文件夹的部分启用(背景为宽斜杠白条)

// TODO: 查看未安排位置的模组的界面
// TODO: 配置是否将未安排位置的模组自动安排在根目录, 或安排到某个目录
// TODO: 配置是否将未安排位置的模组显示在根目录, 或显示在某个目录
// TODO: 标记未安排位置的模组

// TODO: 在有文件夹和模组的排序时仍可以自定义顺序
// TODO: 块状排列 (ConciseModList: ???)
// TODO: Mouse4 和 Mouse5 撤回与回退 返回上级 (Alt + ←→↑)
// TODO: alt 收藏模组 (收藏的模组一般不会因禁用全部按钮而被禁用) (金光闪闪和粒子)
// TODO: 启用某个玩家对应的模组
// TODO: 收藏的特效
// TOTEST: 测试整合包

// TODO: 更多按钮的按钮
// TODO: 按钮: 将已启用的模组复制到此文件夹下 (按住 Shift 将此文件夹下的内容更新为已启用的模组 (二次确认)(不允许根目录执行此操作))
// TODO: 按钮: 批量删除
// TODO: 按钮: 批量重新订阅
// TODO: 按钮: 复制与粘贴
// TODO: 按钮: 禁用多余前置(有被依赖的模组且这些模组都没有启用的模组)

public class ModFolder : Mod {
    public static ModFolder Instance { get; private set; } = null!;

    public override void Load() {
        Instance = this;
        FolderDataSystem.Reload();
        On_UIWorkshopHub.OnInitialize += On_UIWorkshopHub_OnInitialize;
        On_UIWorkshopHub.Click_OpenModsMenu += On_UIWorkshopHub_Click_OpenModsMenu;
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
    private static bool _openOriginModsMenu;
    private void On_UIWorkshopHub_OnInitialize(On_UIWorkshopHub.orig_OnInitialize orig, UIWorkshopHub self) {
        orig(self);
        var buttonMods = self._buttonMods;
        buttonMods.OnMouseOver += (_, _) => {
            // TODO: 这里的文字按照配置修改
            self._descriptionText.SetText(string.Join(' ', self._descriptionText.Text, Instance.GetLocalization("UI.Buttons.Mods.DescriptionToAdd").Value));
        };
        buttonMods.OnRightClick += (e, el) => {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            if (CommonConfig.Instance.LeftClickToEnterFolderSystem) {
                _openOriginModsMenu = true;
                self.Click_OpenModsMenu(e, el);
                _openOriginModsMenu = false;
            }
            else {
                UIModFolderMenu.EnterFrom(self);
            }
        };
    }
    private void On_UIWorkshopHub_Click_OpenModsMenu(On_UIWorkshopHub.orig_Click_OpenModsMenu orig, UIWorkshopHub self, UIMouseEvent evt, UIElement listeningElement) {
        if (_openOriginModsMenu || !CommonConfig.Instance.LeftClickToEnterFolderSystem) {
            orig(self, evt, listeningElement);
        }
        else {
            UIModFolderMenu.EnterFrom(self);
        }
    }

    #endregion
}
