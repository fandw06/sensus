﻿using SensusService;
using SensusService.Probes;
using Xamarin.Forms;

namespace SensusUI
{
    public class SensusNavigationPage : NavigationPage
    {
        public SensusNavigationPage()
            : base(new MainPage())
        {
            #region main page
            MainPage.ProtocolsTapped += async (o, e) =>
                {                  
                    await PushAsync(new ProtocolsPage());
                };

            MainPage.OptionsTapped += async (o, e) =>
                {
                    await PushAsync(new OptionsPage(UiBoundSensusServiceHelper.Get()));
                };
            #endregion

            #region options page
            OptionsPage.ViewLogTapped += async (o, e) =>
                {
                    await PushAsync(new ViewLogPage());
                };
            #endregion

            #region view log page
            ViewLogPage.RefreshTapped += async (o, e) =>
                {
                    await PopAsync();
                    await PushAsync(new ViewLogPage());
                };
            #endregion

            #region protocols page
            ProtocolsPage.EditProtocol += async (o, e) =>
                {
                    await PushAsync(new ProtocolPage(o as Protocol));
                };
            #endregion

            #region protocol page
            ProtocolPage.EditDataStoreTapped += async (o, e) =>
                {
                    if (e.DataStore != null)
                        await PushAsync(new DataStorePage(e));
                };

            ProtocolPage.CreateDataStoreTapped += async (o, e) =>
                {
                    await PushAsync(new CreateDataStorePage(e));
                };

            ProtocolPage.ProbeTapped += async (o, e) =>
                {
                    await PushAsync(new ProbePage(e.Item as Probe));
                };
            #endregion

            #region create data store page
            CreateDataStorePage.CreateTapped += async (o, e) =>
                {
                    await PopAsync();
                    await PushAsync(new DataStorePage(e));
                };
            #endregion

            #region data store page
            DataStorePage.OkTapped += async (o, e) =>
                {
                    await PopAsync();
                };
            #endregion
        }
    }
}
