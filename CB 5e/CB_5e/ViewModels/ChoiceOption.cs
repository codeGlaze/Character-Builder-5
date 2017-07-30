﻿using CB_5e.Helpers;
using OGL;
using OGL.Common;
using OGL.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace CB_5e.ViewModels
{
    public class ChoiceOption: ObservableObject
    {
        private IXML value;
        public IXML Value { get => value; set => SetProperty(ref this.value, value); }
        public ChoiceViewModel Model { get; set; }
        public Feature Feature { get; set; }
        public string Name { get => Value.ToString(); }
        private bool selected;
        public bool Selected { get => selected; set => SetProperty(ref this.selected, value, "", () => OnPropertyChanged("SelectedColor")); }
        public virtual Color SelectedColor { get => Selected ? Color.DarkBlue : Color.Default; }
        public Command Select { get => Model.OnSelect; }
        public virtual string NameWithSource
        {
            get
            {
                bool old = ConfigManager.AlwaysShowSource;
                ConfigManager.AlwaysShowSource = true;
                string res = Value.ToString();
                ConfigManager.AlwaysShowSource = old;
                return res;
            }
        }

    }
}
