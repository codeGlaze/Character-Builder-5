﻿using CB_5e.Services;
using CB_5e.ViewModels.Modify;
using CB_5e.Views.Modify.Collections;
using OGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace CB_5e.Views.Modify
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Compendium : ContentPage
    {
        public List<string> Items { get; set; } = new List<string>()
        {
            "Races", "Subraces",
            "Classes", "Subclasses",
            "Backgrounds",
            "Standalone Features",
            "Spells",
            "Skills",
            "Languages",
            "Items",
            "Magic Items",
            "Conditions",
            "Level",
            "Ability Scores",
            "Settings"
        };
        public Compendium()
        {
            Title = "Compendium";
            InitializeComponent();
            BindingContext = this;
        }

        private async void ListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is string s)
            {
                if (s == "Races") await Navigation.PushAsync(new CompendiumRacesPage());
                else if (s == "Subraces") await Navigation.PushAsync(new CompendiumSubRacesPage());
                else if (s == "Classes") await Navigation.PushAsync(new CompendiumClassesPage());
                else if (s == "Subclasses") await Navigation.PushAsync(new CompendiumSubClassPage());
                else if (s == "Skills") await Navigation.PushAsync(new CompendiumSkillsPage());
                else if (s == "Backgrounds") await Navigation.PushAsync(new CompendiumBackgroundsPage());
                else if (s == "Conditions") await Navigation.PushAsync(new CompendiumConditionsPage());
                else if (s == "Spells") await Navigation.PushAsync(new CompendiumSpellsPage());
                else if (s == "Languages") await Navigation.PushAsync(new CompendiumLanguagesPage());
                else if (s == "Items") await Navigation.PushAsync(new CompendiumItemsOverviewPage());
                else if (s == "Standalone Features") await Navigation.PushAsync(new CompendiumFeatsOverviewPage());
                else if (s == "Magic Items") await Navigation.PushAsync(new CompendiumMagicOverviewPage());
                else if (s == "Level")
                {
                    OGLContext context = new OGLContext();
                    await PCLSourceManager.InitAsync();
                    ConfigManager config = await context.LoadConfigAsync(await PCLSourceManager.Data.GetFileAsync("Config.xml"));
                    DependencyService.Get<IHTMLService>().Reset(config);
                    await context.LoadLevelAsync(await PCLSourceManager.Data.GetFileAsync(config.Levels));
                    LevelEditModel m = new LevelEditModel(context);
                    TabbedPage t = new TabbedPage();
                    t.Children.Add(new NavigationPage(new IntListPage(m, "Experience", "Level ", "0", Keyboard.Numeric, true, true)) { Title = "Experience" });
                    t.Children.Add(new NavigationPage(new IntListPage(m, "Proficiency", "Level ", "+#;-#;0", Keyboard.Telephone, true, true)) { Title = "Proficiency" });
                    m.TrackChanges = true;
                    await Navigation.PushModalAsync(t);
                }
                else if (s == "Ability Scores")
                {
                    OGLContext context = new OGLContext();
                    await PCLSourceManager.InitAsync();
                    ConfigManager config = await context.LoadConfigAsync(await PCLSourceManager.Data.GetFileAsync("Config.xml"));
                    DependencyService.Get<IHTMLService>().Reset(config);
                    await context.LoadAbilityScoresAsync(await PCLSourceManager.Data.GetFileAsync(config.AbilityScores));
                    ScoresEditModel m = new ScoresEditModel(context);
                    TabbedPage t = new TabbedPage();
                    t.Children.Add(new NavigationPage(new EditScores(m)) { Title = "Scores" });
                    t.Children.Add(new NavigationPage(new StringListPage(m, "Arrays", null, true)) { Title = "Arrays" });
                    t.Children.Add(new NavigationPage(new IntListPage(m, "PointBuyCost", "Minimum -1+", "0 points", Keyboard.Telephone, true, false)) { Title = "Point Buy Cost" });
                    m.TrackChanges = true;
                    await Navigation.PushModalAsync(t);
                }
            }
            (sender as ListView).SelectedItem = null;
        }
    }
}