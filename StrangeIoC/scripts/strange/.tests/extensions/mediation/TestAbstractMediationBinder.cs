using System;
using NUnit.Framework;
using strange.framework.api;
using strange.extensions.injector.impl;
using strange.extensions.mediation.impl;
using strange.extensions.mediation.api;
using System.Collections.Generic;


namespace strange.unittests
{
	[TestFixture()]
	public class TestAbstractMediationBinder
	{
		private TestMediationBinder mediationBinder;
		private InjectionBinder injectionBinder;


		[SetUp]
		public void SetUp()
		{
			injectionBinder = new InjectionBinder ();
			mediationBinder = new TestMediationBinder ();
			mediationBinder.injectionBinder = injectionBinder;
		}

		[Test]
		public void TestRawBindingIsMediationBinding()
		{
			IBinding binding = mediationBinder.GetRawBinding ();
			Assert.IsInstanceOf<IMediationBinding> (binding);
		}

		[Test]
		public void TestAwakeTriggersMappingAndInjection()
		{
			mediationBinder.Bind<TestView> ().To<TestMediator> ();
			injectionBinder.Bind<ClassToBeInjected> ().To<ClassToBeInjected> ();

			TestView view = new TestView ();
			mediationBinder.Trigger (MediationEvent.AWAKE, view);

			Assert.IsTrue (view.registeredWithContext);
			Assert.IsNotNull (view.testInjection);
			TestMediator mediator = mediationBinder.mediators [view] as TestMediator;
			Assert.AreEqual (1, mediationBinder.mediators.Count);
			Assert.IsNotNull (mediator);
			Assert.IsInstanceOf<TestMediator> (mediator);

			Assert.IsTrue (mediator.preregistered);
			Assert.IsTrue (mediator.registered);
			Assert.IsFalse (mediator.removed);
		}

		[Test]
		public void TestAwakeTriggersInjectionForUnmappedView()
		{
			injectionBinder.Bind<ClassToBeInjected> ().To<ClassToBeInjected> ();

			TestView view = new TestView ();
			mediationBinder.Trigger (MediationEvent.AWAKE, view);

			Assert.IsNotNull (view.testInjection);
			TestMediator mediator = null;
			if (mediationBinder.mediators.ContainsKey(view))
			{
				mediator = mediationBinder.mediators [view] as TestMediator;
			}
			Assert.AreEqual (0, mediationBinder.mediators.Count);
			Assert.IsNull (mediator);
		}

		[Test]
		public void TestDestroyedTriggersUnmapping()
		{
			mediationBinder.Bind<TestView> ().To<TestMediator> ();
			injectionBinder.Bind<ClassToBeInjected> ().To<ClassToBeInjected> ();

			TestView view = new TestView ();
			mediationBinder.Trigger (MediationEvent.AWAKE, view);

			TestMediator mediator = mediationBinder.mediators [view] as TestMediator;

			mediationBinder.Trigger (MediationEvent.DESTROYED, view);
			Assert.IsTrue (mediator.removed);
			Assert.AreEqual (0, mediationBinder.mediators.Count);
		}

		[Test]
		public void TestErrorIfClassMappedToItself()
		{
			mediationBinder.Bind<TestView> ().To<TestView> ();
			injectionBinder.Bind<ClassToBeInjected> ().To<ClassToBeInjected> ();

			TestView view = new TestView ();

			TestDelegate testDelegate = delegate
			{
				mediationBinder.Trigger (MediationEvent.AWAKE, view);
			};
			MediationException ex = Assert.Throws<MediationException>(testDelegate); //Because we've mapped view to self
			Assert.AreEqual (MediationExceptionType.MEDIATOR_VIEW_STACK_OVERFLOW, ex.type);
		}

		[Test]
		public void TestInjectViews()
		{
			injectionBinder.Bind<ClassToBeInjected>().To<ClassToBeInjected>();

			TestView view = new TestView();
			IView one = new TestView();
			IView two = new TestView();
			IView three = new TestView();

			IView[] views =
			{
				view,
				one,
				two,
				three
			};

			view.Views = views;

			mediationBinder.TestInjectViewAndChildren(view);

			Assert.AreEqual(true, one.registeredWithContext);
			Assert.AreEqual(true, two.registeredWithContext);
			Assert.AreEqual(true, three.registeredWithContext);

		}
	}


	class TestMediationBinder : AbstractMediationBinder
	{

		public Dictionary<IView, IMediator> mediators = new Dictionary<IView, IMediator>();
		
		protected override IView[] GetViews(IView view)
		{
			TestView testView = view as TestView;
			return testView.Views;
		}

		protected override bool HasMediator(IView view, Type mediatorType)
		{
			TestView testView = view as TestView;
			return testView.HasMediator;
		}

		override protected object CreateMediator(IView view, Type mediatorType)
		{
			IMediator mediator = new TestMediator ();
			mediators.Add (view, mediator);
			return mediator;
		}

		override protected object DestroyMediator(IView view, Type mediatorType)
		{
			IMediator mediator = null;
			if (mediators.ContainsKey(view))
			{
				mediator = mediators[view];
				mediators.Remove(view);
				mediator.OnRemove ();
			}
			return mediator;
		}

		protected override void ThrowNullMediatorError (Type viewType, Type mediatorType)
		{
			throw new MediationException("The view: " + viewType.ToString() + " is mapped to mediator: " + mediatorType.ToString() + ". AddComponent resulted in null, which probably means " + mediatorType.ToString().Substring(mediatorType.ToString().LastIndexOf(".") + 1) + " is not a MonoBehaviour.", MediationExceptionType.NULL_MEDIATOR);
		}

		public void TestInjectViewAndChildren(IView view)
		{
			InjectViewAndChildren(view);
		}
	}

	class TestView : IView
	{
		[Inject]
		public ClassToBeInjected testInjection { get; set; }

		public TestView()
		{
			Views = new IView[]
			{
				this
			};
		}

		#region IView implementation

		private bool _requiresContext;
		private bool _registeredWithContext;

		public IView[] Views = {};
		public bool HasMediator = false;

		public bool requiresContext
		{
			get
			{
				return _requiresContext;
			}
			set
			{
				_requiresContext = value;
			}
		}

		public bool registeredWithContext
		{
			get
			{
				return _registeredWithContext;
			}
			set
			{
				_registeredWithContext = value;
			}
		}

		public bool autoRegisterWithContext
		{
			get
			{
				return true;
			}
		}

		#endregion
	}

	class TestMediator : IMediator
	{
		public bool preregistered = false;
		public bool registered = false;
		public bool removed = false;

		#region IMediator implementation

		public void PreRegister ()
		{
			preregistered = true;
		}

		public void OnRegister ()
		{
			registered = true;
		}

		public void OnRemove ()
		{
			removed = true;
		}

		public UnityEngine.GameObject contextView
		{
			get
			{
				throw new NotImplementedException ();
			}
			set
			{
				throw new NotImplementedException ();
			}
		}

		#endregion
	}
}
