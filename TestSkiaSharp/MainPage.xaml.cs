using System.ComponentModel;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;

namespace TestSkiaSharp
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        public MainPage() {
            InitializeComponent();
        }

        private void OnTouchEvent(object sender, SKTouchEventArgs e) {
            System.Diagnostics.Debug.Print($">>>>> TOUCH EVENT: {e.ActionType}");
            e.Handled = true;
        }
    }
}
