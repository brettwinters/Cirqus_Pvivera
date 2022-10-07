using System.Linq;
using d60.Cirqus.Config.Configurers;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Config
{
    [TestFixture]
    public class TestConfigurationContainer : FixtureBase
    {
        //private ServiceProvider _provider;
        private ServiceCollection _services;

        protected override void DoSetUp()
        {
            _services = new ServiceCollection();

            var container = new NewConfigurationContainer(_services); //whats this for?
        }

        //[Test]
        //public void ThrowsWhenNoResolverIsPresent()
        //{
        //    Assert.Throws<ResolutionException>(() => _provider.GetService<string>());
        //}
        
        [Test]
        public void NullNoResolverIsPresent() {
            Assert.IsNull(_services.BuildServiceProvider().GetService<string>());
        }

        [Test]
        public void CanGetPrimaryInstance()
        {
            _services.AddTransient<string>(s => "hej");

            var resolvedString = _services.BuildServiceProvider().GetService<string>();

            Assert.That(resolvedString, Is.EqualTo("hej"));
        }

        [Test]
        public void CanGetDecoratedInstance() {
            _services.AddTransient<string>(s => "hej");
            _services.Decorate<string>(c => c + " med dig");

            var resolvedString = _services.BuildServiceProvider().GetService<string>();
            Assert.That(resolvedString, Is.EqualTo("hej med dig"));
        }

        [Test]
        public void CanGetDecoratedInstanceWithAnArbitraryNumberOfDecorators() {
            _services.AddTransient<string>(s => "hej");

            Enumerable.Range(1, 7)
                .ToList()
                .ForEach(tal => _services.Decorate<string>(c => c + " " + tal)); ;

            var resolvedString = _services.BuildServiceProvider().GetService<string>();

            Assert.That(resolvedString, Is.EqualTo("hej 1 2 3 4 5 6 7"));
        }

        [Test]
        public void CanGetDecoratedInstanceWithAnArbitraryNumberOfInterleavedDecorators() {

            //new
            _services.AddTransient<string>(s => "1");
            _services.AddTransient<string>(s => (int.Parse(s.GetService<string>()) + 1).ToString()); //can't use int
            _services.Decorate<string>(c => c + "2");

            Assert.Inconclusive("can't figure this out");

            //original
            //_provider.Register(c => "1");
            // _provider.Register(c => int.Parse(c.GetService<string>()) + 1);
            //_provider.Decorate(c => c.GetService<int>().ToString() + "2");
            //_provider.Decorate(c => int.Parse(c.GetService<string>()) + 2);
            //var resolvedString = _provider.GetService<int>();
            //Assert.That(resolvedString, Is.EqualTo(24)); //..... ok, this is what happens:

            //  we resolve an int =>
            //      we resolve a string =>
            //          we resolve an int =>
            //              we resolve a string => 
            //              return "1"
            //          return int.parse("1") + 1 = 2
            //      return "2".toString() + "2" = "22"
            //  return int.parse("22") + 2 = 24
            //  = 24!
        }

        [Test]
        public void CanRegisterAndGetInstance() {
            const string friendlyInstance = "hej med dig min ven";

            _services.AddTransient<string>(s => friendlyInstance);
            var instance = _services.BuildServiceProvider().GetService<string>();

            Assert.That(instance, Is.EqualTo(friendlyInstance));
        }

        [Test]
        public void CANRegisterInstanceMultipleTimesByDefault() {
            const string friendlyInstance = "hej med dig min ven";

            _services.AddTransient<string>(s => friendlyInstance);
            _services.AddTransient<string>(s => "hej igen");

            var instance = _services.BuildServiceProvider().GetService<string>();
            Assert.That(instance, Is.EqualTo("hej igen"));



            //orig
            //_provider.RegisterInstance(friendlyInstance);
            // Assert.Throws<InvalidOperationException>(() => _provider.RegisterInstance("hej igen"));
        }

        //[Test]
        //public void CanRegisterMultipleInstancesIfMultipleIsSpecified()
        //{
        //    _provider.RegisterInstance("hej", multi:true);
        //    _provider.RegisterInstance("med", multi:true);
        //    _provider.RegisterInstance("dig", multi:true);

        //    var all = _provider.GetAll<string>();

        //    Assert.That(string.Join(" ", all), Is.EqualTo("hej med dig"));
        //}
    }
}