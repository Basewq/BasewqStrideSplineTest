using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Input;
using System.Collections.Generic;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Events;
using Stride.UI.Panels;

namespace SplineTools
{
    public class SwitchScene : SyncScript
    {
        public List<UrlReference<Scene>> Scenes = new List<UrlReference<Scene>>();
        public bool ShowMenu = true;
        private UIPage activePage;
        private Button btnMenu;
        private Canvas mainMenu;
        private StackPanel buttonsStartUI;
        private StackPanel buttonsCompletedUI;

        public override void Start()
        {
            activePage = Entity.Get<UIComponent>().Page;
            mainMenu = activePage.RootElement.FindVisualChildOfType<Canvas>("MainPage");
            buttonsStartUI = activePage.RootElement.FindVisualChildOfType<StackPanel>("SceneButtons");

            // Create buttons for the first scene
            if (buttonsStartUI.Children.Count == 1)
            {
                Game.Window.IsMouseVisible = true;

                btnMenu = activePage.RootElement.FindVisualChildOfType<Button>("BtnSceneToLoad");
                btnMenu.Click += BtnMenuClicked;

                CreateButtons();
            }
            else
            {
                mainMenu.Visibility = Visibility.Hidden;
            }

            if (ShowMenu)
            {
                buttonsStartUI.Visibility = Visibility.Visible;
            }
        }

        public override void Update()
        {
            if (Input.IsKeyPressed(Keys.Escape))
            {
                mainMenu.Visibility = mainMenu.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;

            }
        }

        private void CreateButtons()
        {
            var startButton = buttonsStartUI.Children[0] as Button;
            var startText = startButton.VisualChildren[0] as TextBlock;
            startButton.Visibility = Visibility.Hidden;
            CreateButton(startButton, startText, Scenes, buttonsStartUI);
            buttonsStartUI.Children.Remove(startButton);
        }

        private void CreateButton(Button baseButtonButton, TextBlock textBlock, List<UrlReference<Scene>> tutorialScenes, StackPanel stackPanel)
        {
            foreach (var keyPair in tutorialScenes)
            {
                var button = new Button
                {

                    Content = new TextBlock
                    {
                        Font = textBlock.Font,
                        TextSize = textBlock.TextSize,
                        Height = baseButtonButton.Content.Height,
                        Text = keyPair.Url.Replace("Scenes/", ""),
                        TextColor = Color.White,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    Height = baseButtonButton.Height,
                    NotPressedImage = baseButtonButton.NotPressedImage,
                    PressedImage = baseButtonButton.PressedImage,
                    MouseOverImage = baseButtonButton.MouseOverImage,

                };
                button.Click += (sender, e) => BtnLoadTutorial(sender, e, keyPair);

                stackPanel.Children.Add(button);
            }
        }

        private void BtnMenuClicked(object sender, RoutedEventArgs e)
        {
            mainMenu.Visibility = mainMenu.IsVisible ? Visibility.Hidden : Visibility.Visible;
            buttonsStartUI.Visibility = buttonsStartUI.IsVisible ? Visibility.Hidden : Visibility.Visible;
            buttonsCompletedUI.Visibility = buttonsCompletedUI.IsVisible ? Visibility.Hidden : Visibility.Visible;

            if (mainMenu.Visibility == Visibility.Visible)
            {
                Game.Window.IsMouseVisible = true;
            }
        }

        private void BtnLoadTutorial(object sender, RoutedEventArgs e, UrlReference<Scene> newTutorialScene)
        {
            Content.Unload(SceneSystem.SceneInstance.RootScene);
            SceneSystem.SceneInstance.RootScene = Content.Load(newTutorialScene);
        }
    }
}
