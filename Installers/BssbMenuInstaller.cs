using ServerBrowser.Core;
using ServerBrowser.UI;
using ServerBrowser.UI.Views;
using Zenject;

namespace ServerBrowser.Installers
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class BssbMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<ModeSelectionIntegrator>().AsSingle();
            
            Container.BindInterfacesAndSelfTo<CreateServerExtender>().AsSingle();

            Container.Bind<ServerBrowserMainViewController>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<ServerBrowserDetailViewController>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<ServerBrowserFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            
            Container.BindInterfacesAndSelfTo<BssbMenuDataCollector>().AsSingle();
        }
    }
}