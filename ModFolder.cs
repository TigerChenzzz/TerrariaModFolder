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
global using TigerUtilsLib;
global using static TigerUtilsLib.TigerClasses;
global using static TigerUtilsLib.TigerUtils;
using ModFolder.Configs;
using ModFolder.Systems;
using ModFolder.UI.Menu;
using Terraria.Audio;
using Terraria.GameContent.UI.States;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModFolder;

// BUG: 启用时可能启用到过期的模组?
// BUG: 强制需求配置更改的模组可能不会引发重新加载? (关于 UIModItemInFolder._configChangesRequireReload 的问题)
// BUG: Generate 之后收藏等信息丢失

// TODO: 检查更新按钮 (提示有哪些模组需要更新? 筛选需要更新的模组?) / 更新全部 / 单个模组更新 (代码参见 UIModBrowser)
// TODO: 筛选是否已订阅, 筛选模组位置 (steam, 本地, 整合包)
// TODO: 显示全部模组中也显示未订阅的

// TODO: "单击以禁用 ? 和 ? 个依赖模组" (hjson 中的 ModsDisableAndDependents, ModsEnableAndDependencies)
// TODO: 一键订阅按钮
// TODO: 显示所有下载项
// TODO: 取消订阅时保留 description 等信息(description, 图片, build.txt) (可选 (对于每次取消订阅, 而不是可配置))
// TODO: Generate 修缮, 只在必要时重新生成
// TODO: 复制文件夹
// TODO: 文件夹快捷方式
// TODO: 批量 重新订阅 / 删除 (删除索引 / 取消订阅 / Both)    本文件夹下 / 本文件夹下及所有子文件夹下 (alt 控制)
// TODO: 关于各处二次确认和 ctrl shift alt 的联动: 二次确认界面有三个提示指示分别有什么用, 按下对应键时对应提示亮起且此时按确认时才会有对应效果
// TODO: 大小模组图标的配置
// TODO: 新建文件夹排序问题
// TODO: description 的本地化支持?
// TODO: 模组的内部名如何查看?
// TODO: 选中, 选中多个
//           按住 Ctrl 时右键以选中或取消选中一个, 按住 Shift 右键以选中多个, 按住 Ctrl 和 Shift 以反选多个
//           选中多个可同时移动或通过按钮复制粘贴删除重新订阅
// TODO: 名称过长时的处理 (滚动? 省略?)
// TODO: 弱依赖显示
// TODO: 尝试强制更新已有模组
// TODO: 下载缺失依赖
// TODO: 搜索文件夹
// TODO: 按特定键双击文件夹以启用 / 禁用所有内含模组 (禁用冗余依赖?)
// TODO: 文件夹的模组启用状态剔除重复的模组

// TODO: 右键拖动时禁用左键?

// TODO: 添加按钮是否筛选文件夹
// TODO: 分组与筛选: 按类型 (客户端 / 服务端 / 文件夹), 按 Steam 还是本地文件, 按收藏, 筛选最近更新与新添加

// TODO: 文件夹的最近更新属性

// TODO: 搜索全部时同时搜索文件夹, 同时展示文件夹或模组的位置 (文件夹树中)
//           搜索当前文件夹下的所有模组, 显示其位置
//           使用特殊符号搜索作者等信息 ("@ <作者>", 自动剔除空格(如果作者名字里也有@的话...), "@@"表示普通@, 此举同时可以搜索作者id (最近添加的, 需要17位完整的id))

// TODO: 查看未安排位置的模组的界面
// TODO: 配置是否将未安排位置的模组自动安排在根目录, 或安排到某个目录
// TODO: 配置是否将未安排位置的模组显示在根目录, 或显示在某个目录
// TODO: 标记未安排位置的模组

// TODO: 在有文件夹和模组的排序时仍可以自定义顺序
// TODO: 块状排列 (ConciseModList: ???)
//           在左上角有切换块状和条状的按钮
//           当在块状时当鼠标悬浮在此按钮上时在按钮上方额外显示滑条和输入框 (或者几个特定的数字?) 以调整大小
//           或者也可以调整条状显示时的大小?
// TODO: Mouse4 和 Mouse5 撤回与回退 返回上级 (Alt + ←→↑)
// TODO: 启用某个玩家对应的模组
// TODO: 收藏的特效
// TOTEST: 测试整合包
// TODO: 如何可以删除已启用的模组?

// TODO: 按钮的悬浮提示和 Ctrl Shift Alt 提示更加明显
// TODO: 按钮的展开
//           配置按钮, 展开为配置版 (直接使用按钮来配置)
//           启用与禁用按钮展开, 选择是否影响全部, 包含子文件夹还是仅此文件夹, 等等原 Ctrl, Shift 和 Alt 做的事情 (原快捷键仍然生效, 且按下快捷键时对应按钮也会有反应),
//               原工具提示放在展开的板上
// TODO: 下载缺失依赖
//           详细展示缺失哪些依赖, 哪些现存模组依赖于它
//           自选下载哪些依赖


public class ModFolder : Mod {
    public static ModFolder Instance { get; private set; } = null!;

    public override void Load() {
        InitializeTigerUtils(this);
        Instance = this;
        FolderDataSystem.Reload();
        On_UIWorkshopHub.OnInitialize += On_UIWorkshopHub_OnInitialize;
        On_UIWorkshopHub.Click_OpenModsMenu += On_UIWorkshopHub_Click_OpenModsMenu;
        MonoModHooks.Add(typeof(Interface).GetMethod(nameof(Interface.ModLoaderMenus), TMLReflection.bfs), On_Interface_ModLoaderMenus);
        var configList = Interface.modConfigList;
        if (!configList._isInitialized) {
            configList.Initialize();
        }
        if (Interface.modConfigList.backButton is { } backButton) {
            backButton.OnLeftClick += UIModConfigList_BackButton_OnLeftClick;
        }
    }

    public override void Unload() {
        if (Instance == null) {
            // 这样能防止大多数情况报错, 但是由于可能断在一些奇怪的地方, 所以下面仍然要安全检查
            return;
        }
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
            Main.menuMode = MenuID.FancyUI; // 888
        }
    }
    #endregion
    #region 在创意工坊页面添加右键进入的实现与提示
    private static bool _openOriginModsMenu;
    private void On_UIWorkshopHub_OnInitialize(On_UIWorkshopHub.orig_OnInitialize orig, UIWorkshopHub self) {
        orig(self);
        var buttonMods = self._buttonMods;
        buttonMods.OnMouseOver += (_, _) => {
            var localizationKey = CommonConfig.Instance.LeftClickToEnterFolderSystem ? "UI.Buttons.Mods.DescriptionWhenLeftClick" : "UI.Buttons.Mods.DescriptionToAdd";
            self._descriptionText.SetText(string.Join(' ', self._descriptionText.Text, Instance.GetLocalization(localizationKey).Value));
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
