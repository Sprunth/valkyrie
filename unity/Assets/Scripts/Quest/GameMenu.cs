using UnityEngine;
using Assets.Scripts.Content;
using Assets.Scripts.UI.Screens;
using Assets.Scripts.UI;

// In quest game menu
public class GameMenu {
    private static readonly StringKey SAVE = new StringKey("val","SAVE");
    private static readonly StringKey MAIN_MENU = new StringKey("val", "MAIN_MENU");
    private static readonly StringKey UNDO = new StringKey("val", "UNDO");
    private static readonly StringKey NARRATION = new StringKey("val", "NARRATION");
    private static readonly StringKey ON = new StringKey("val", "ON");
    private static readonly StringKey OFF = new StringKey("val", "OFF");

    // Open the menu
    public static void Create()
    {
        Game game = Game.Get();
        if (GameObject.FindGameObjectWithTag(Game.DIALOG) != null)
        {
            return;
        }

        if (GameObject.FindGameObjectWithTag(Game.ACTIVATION) != null)
        {
            return;
        }

        // Take screen shot for save before menu is drawn
        game.cc.TakeScreenshot(delegate { Draw(); });
    }

    public static void Draw()
    {
        Game game = Game.Get();
        bool narration = game.tts.ModelInstalled;
        // Border around menu items
        UIElement ui = new UIElement();
        ui.SetLocation((UIScaler.GetWidthUnits() - 12) / 2, 6, 12, narration ? 16 : 13);
        new UIElementBorder(ui);

        ui = new UIElement();
        ui.SetLocation((UIScaler.GetWidthUnits() - 10) / 2, 7, 10, 2);
        ui.SetText(UNDO);
        ui.SetBGColor(new Color(0.03f, 0.0f, 0f));
        ui.SetFontSize(UIScaler.GetMediumFont());
        ui.SetFont(game.gameType.GetHeaderFont());
        ui.SetButton(delegate { Undo(); });
        new UIElementBorder(ui);

        ui = new UIElement();
        ui.SetLocation((UIScaler.GetWidthUnits() - 10) / 2, 10, 10, 2);
        ui.SetText(SAVE);
        ui.SetBGColor(new Color(0.03f, 0.0f, 0f));
        ui.SetFontSize(UIScaler.GetMediumFont());
        ui.SetFont(game.gameType.GetHeaderFont());
        ui.SetButton(delegate { Save(); });
        new UIElementBorder(ui);

        ui = new UIElement();
        ui.SetLocation((UIScaler.GetWidthUnits() - 10) / 2, 13, 10, 2);
        if (game.testMode)
        {
            ui.SetText(new StringKey("val", "EDITOR"));
            ui.SetButton(delegate { Editor(); });
        }
        else
        {
            ui.SetText(MAIN_MENU);
            ui.SetButton(delegate { Quit(); });
        }
        ui.SetBGColor(new Color(0.03f, 0.0f, 0f));
        ui.SetFontSize(UIScaler.GetMediumFont());
        ui.SetFont(game.gameType.GetHeaderFont());
        new UIElementBorder(ui);

        if (narration)
        {
            ui = new UIElement();
            ui.SetLocation((UIScaler.GetWidthUnits() - 10) / 2, 16, 10, 2);
            ui.SetText(NARRATION.Translate() + ": " + (game.tts.Enabled ? ON : OFF).Translate());
            ui.SetBGColor(new Color(0.03f, 0.0f, 0f));
            ui.SetFontSize(UIScaler.GetMediumFont());
            ui.SetFont(game.gameType.GetHeaderFont());
            ui.SetButton(delegate { game.tts.Enabled = !game.tts.Enabled; Destroyer.Dialog(); Draw(); });
            new UIElementBorder(ui);
        }

        ui = new UIElement();
        ui.SetLocation((UIScaler.GetWidthUnits() - 10) / 2, narration ? 19 : 16, 10, 2);
        ui.SetText(CommonStringKeys.CANCEL);
        ui.SetBGColor(new Color(0.03f, 0.0f, 0f));
        ui.SetFontSize(UIScaler.GetMediumFont());
        ui.SetFont(game.gameType.GetHeaderFont());
        ui.SetButton(delegate { Destroyer.Dialog(); });
        new UIElementBorder(ui);
    }

    public static void Undo()
    {
        Game game = Game.Get();
        Destroyer.Dialog();
        game.CurrentQuest.Undo();
    }

    public static void Save()
    {
        Destroyer.Dialog();
        new SaveSelectScreen(true);
    }

    public static void Quit()
    {
        Destroyer.Dialog();
        SaveManager.Save(0, true);
    }

    public static void Editor()
    {
        GameStateManager.Editor.EditCurrentQuest();
    }
}
