namespace FakeItEasy.Tests.Core.Creation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using FakeItEasy.Core;
    using FakeItEasy.Core.Creation;
    using NUnit.Framework;
    using System.Diagnostics;
    using FakeItEasy.Expressions;

    [TestFixture]
    public class FakeObjectCreatorTests
    {
        private static readonly Logger logger = Log.GetLogger<FakeObjectCreatorTests>();

        private IProxyGenerator2 proxyGenerator;
        private FakeObjectCreator fakeObjectCreator;
        private IExceptionThrower thrower;
        private IFakeManagerAccessor fakeManagerAttacher;

        [SetUp]
        public void SetUp()
        {
            this.proxyGenerator = A.Fake<IProxyGenerator2>();
            this.thrower = A.Fake<IExceptionThrower>();
            this.fakeManagerAttacher = A.Fake<IFakeManagerAccessor>();

            this.fakeObjectCreator = new FakeObjectCreator(this.proxyGenerator, this.thrower, this.fakeManagerAttacher);
        }

        [Test]
        public void Should_return_fake_when_successful()
        {
            // Arrange
            var argumentsForConstructor = new object[] {};
            
            var options = new FakeOptions
            {
                AdditionalInterfacesToImplement = new Type[] { },
                ArgumentsForConstructor = new object[] { }
            };

            var proxy = A.Fake<IFoo>();

            A.CallTo(() => this.proxyGenerator.GenerateProxy(typeof(IFoo), options.AdditionalInterfacesToImplement, options.ArgumentsForConstructor))
                .Returns(new ProxyGeneratorResult(proxy, A.Dummy<ICallInterceptedEventRaiser>()));

            // Act
            var createdFake = this.fakeObjectCreator.CreateFake(typeof(IFoo), options, A.Dummy<IDummyValueCreationSession>(), throwOnFailure: false);

            // Assert
            Assert.That(createdFake, Is.SameAs(proxy));
        }
        
        [Test]
        public void Should_attach_fake_manager_to_proxy_when_successful()
        {
            // Arrange
            var proxy = A.Fake<IFoo>();
            var eventRaiser = A.Fake<ICallInterceptedEventRaiser>();

            A.CallTo(() => this.proxyGenerator.GenerateProxy(typeof(IFoo), A<IEnumerable<Type>>.Ignored.Argument, A<IEnumerable<object>>.Ignored.Argument))
                .Returns(new ProxyGeneratorResult(proxy, eventRaiser));

            // Act
            this.fakeObjectCreator.CreateFake(typeof(IFoo), FakeOptions.Empty, A.Dummy<IDummyValueCreationSession>(), throwOnFailure: false);

            // Assert
            A.CallTo(() => this.fakeManagerAttacher.AttachFakeManagerToProxy(proxy, eventRaiser)).MustHaveHappened();
        }

        [Test]
        public void Should_throw_when_generator_fails_and_arguments_for_constructor_are_specified()
        {
            // Arrange
            this.StubProxyGeneratorToFail("fail reason");

            var options = new FakeOptions
            {
                ArgumentsForConstructor = new object[] { "argument for constructor " }
            };

            // Act
            this.fakeObjectCreator.CreateFake(typeof(IFoo), options, A.Dummy<IDummyValueCreationSession>(), throwOnFailure: true);
            
            // Assert
            A.CallTo(() => this.thrower.ThrowFailedToGenerateProxyWithArgumentsForConstructor("fail reason"))
                .MustHaveHappened();
        }

        [Test]
        public void Should_return_null_when_unsuccessful_and_throw_on_failure_is_false()
        {
            // Arrange
            this.StubProxyGeneratorToFail();

            // Act
            var createdFake = this.fakeObjectCreator.CreateFake(typeof(IFoo), FakeOptions.Empty, A.Dummy<IDummyValueCreationSession>(), throwOnFailure: false);

            // Assert
            Assert.That(createdFake, Is.Null);
        }

        [Test]
        public void Should_not_throw_when_unsuccessful_and_throw_on_failure_is_false()
        {
            // Arrange
            this.StubProxyGeneratorToFail();

            // Act
            this.fakeObjectCreator.CreateFake(typeof(IFoo), FakeOptions.Empty, A.Dummy<IDummyValueCreationSession>(), throwOnFailure: false);

            // Assert
            Any.CallTo(this.thrower).MustNotHaveHappened();
        }

        [Test]
        public void Should_try_with_resolved_constructors_in_correct_order()
        {
            using (var scope = Fake.CreateScope())
            { 
                // Arrange
                var session = A.Fake<IDummyValueCreationSession>();
                StubSessionWithDummyValue<int>(session, 1);

                this.StubProxyGeneratorToFail();

                var options = new FakeOptions
                {
                    AdditionalInterfacesToImplement = new Type[] { }
                };
                
                // Act
                this.fakeObjectCreator.CreateFake(typeof(TypeWithMultipleConstructors), options, session, throwOnFailure: false);

                // Assert
                using (scope.OrderedAssertions())
                {
                    A.CallTo(() => this.proxyGenerator.GenerateProxy(typeof(TypeWithMultipleConstructors), options.AdditionalInterfacesToImplement, A<IEnumerable<object>>.That.IsNull().Argument))
                       .MustHaveHappened();
                    A.CallTo(() => this.proxyGenerator.GenerateProxy(typeof(TypeWithMultipleConstructors), options.AdditionalInterfacesToImplement, A<IEnumerable<object>>.That.IsThisSequence(1, 1).Argument))
                        .MustHaveHappened();
                    A.CallTo(() => this.proxyGenerator.GenerateProxy(typeof(TypeWithMultipleConstructors), options.AdditionalInterfacesToImplement, A<IEnumerable<object>>.That.IsThisSequence(1).Argument))
                        .MustHaveHappened();
                }
            }
        }

        [Test]
        public void Should_not_try_to_resolve_constructors_when_arguments_for_constructor_are_specified()
        {
            // Arrange
            var session = A.Fake<IDummyValueCreationSession>();
            StubSessionWithDummyValue<int>(session, 1);

            this.StubProxyGeneratorToFail();

            var options = new FakeOptions
            {
                AdditionalInterfacesToImplement = new Type[] { },
                ArgumentsForConstructor = new object[] { 2, 2 }
            };

            // Act
            this.fakeObjectCreator.CreateFake(typeof(TypeWithMultipleConstructors), options, session, throwOnFailure: false);

            // Assert
            A.CallTo(() => this.proxyGenerator.GenerateProxy(typeof(TypeWithMultipleConstructors), options.AdditionalInterfacesToImplement, A<IEnumerable<object>>.That.Not.IsThisSequence(2, 2).Argument))
                .MustNotHaveHappened();
        }

        [Test]
        public void Should_return_first_successfully_generated_proxy()
        {
            // Arrange
            var session = A.Fake<IDummyValueCreationSession>();
            StubSessionWithDummyValue<int>(session, 1);

            var options = new FakeOptions
            {
                AdditionalInterfacesToImplement = new Type[] { }
            };

            var proxy = A.Fake<IFoo>();

            this.StubProxyGeneratorToFail();
            A.CallTo(() => this.proxyGenerator.GenerateProxy(typeof(TypeWithMultipleConstructors), options.AdditionalInterfacesToImplement, A<IEnumerable<object>>.That.IsThisSequence(1, 1).Argument))
                .Returns(new ProxyGeneratorResult(proxy, A.Dummy<ICallInterceptedEventRaiser>()));
            A.CallTo(() => this.proxyGenerator.GenerateProxy(typeof(TypeWithMultipleConstructors), options.AdditionalInterfacesToImplement, A<IEnumerable<object>>.That.IsThisSequence(1).Argument))
                .Returns(new ProxyGeneratorResult(new object(), A.Dummy<ICallInterceptedEventRaiser>()));
                
            // Act
            var createdFake = this.fakeObjectCreator.CreateFake(typeof(TypeWithMultipleConstructors), options, session, throwOnFailure: false);

            // Assert
            Assert.That(createdFake, Is.SameAs(proxy));
        }

        [Test]
        public void Should_not_try_constructor_where_not_all_arguments_are_resolved()
        {
            // Arrange
            var session = A.Fake<IDummyValueCreationSession>();
            StubSessionWithDummyValue<int>(session, 1);
            StubSessionToFailForType<string>(session);

            // Act
            this.fakeObjectCreator.CreateFake(typeof(TypeWithConstructorThatTakesDifferentTypes), FakeOptions.Empty, session, throwOnFailure: false);

            // Assert
            A.CallTo(() => this.proxyGenerator.GenerateProxy(A<Type>.Ignored, A<IEnumerable<Type>>.Ignored.Argument, A<IEnumerable<object>>.That.Not.IsNull().Argument ))
                .MustNotHaveHappened();
        }

        [Test]
        public void Should_try_protected_constructors()
        {
            // Arrange
            var session = A.Fake<IDummyValueCreationSession>();
            StubSessionWithDummyValue<int>(session, 1);

            var options = FakeOptions.Empty;

            // Act
            this.fakeObjectCreator.CreateFake(typeof(TypeWithProtectedConstructor), options, session, throwOnFailure: false);

            // Assert
            A.CallTo(() => this.proxyGenerator.GenerateProxy(typeof(TypeWithProtectedConstructor), options.AdditionalInterfacesToImplement, A<IEnumerable<object>>.That.IsThisSequence(1).Argument))
                .MustHaveHappened();
        }

        [Test]
        public void Should_throw_when_no_resolved_constructor_was_successfully_used()
        {
            // Arrange
            var session = A.Fake<IDummyValueCreationSession>();
            StubSessionToFailForType<int>(session);
            StubSessionToFailForType<string>(session);
            
            this.StubProxyGeneratorToFail("failed");

            // Act
            this.fakeObjectCreator.CreateFake(typeof(TypeWithMultipleConstructors), FakeOptions.Empty, session, throwOnFailure: true);

            // Assert
            var expectedConstructors = new[] 
            {
                new ResolvedConstructor
                {
                    Arguments = new[]
                    {
                        new ResolvedArgument
                        {
                            ArgumentType = typeof(int),
                            ResolvedValue = null,
                            WasResolved = false
                        },
                        new ResolvedArgument
                        {
                            ArgumentType = typeof(int),
                            ResolvedValue = null,
                            WasResolved = false
                        }
                    }
                },
                new ResolvedConstructor
                {
                    Arguments = new[]
                    {
                        new ResolvedArgument
                        {
                            ArgumentType = typeof(int),
                            ResolvedValue = null,
                            WasResolved = false
                        }
                    }
                }
            };
            
            A.CallTo(() => this.thrower.ThrowFailedToGenerateProxyWithResolvedConstructors(typeof(TypeWithMultipleConstructors), "failed", ConstructorsEquivalentTo(expectedConstructors).Argument))
                .MustHaveHappened();
        }

        private ArgumentConstraint<IEnumerable<ResolvedConstructor>> ConstructorsEquivalentTo(IEnumerable<ResolvedConstructor> constructors)
        {
            return A<IEnumerable<ResolvedConstructor>>.That.Matches(x => 
            {
                if (x.Count() != constructors.Count())
                {
                    logger.Debug("Unequal number of constructors.");
                    return false;
                }

                foreach (var constructorPair in x.Zip(constructors))
                {
                    if (constructorPair.First.Arguments.Length != constructorPair.Second.Arguments.Length)
                    {
                        logger.Debug("Unequal number of arguments.");
                        return false;
                    }

                    foreach (var argumentPair in constructorPair.First.Arguments.Zip(constructorPair.Second.Arguments))
                    {
                        var isEqual =
                            object.Equals(argumentPair.First.ArgumentType, argumentPair.Second.ArgumentType)
                            && object.Equals(argumentPair.First.ResolvedValue, argumentPair.Second.ResolvedValue)
                            && argumentPair.First.WasResolved == argumentPair.Second.WasResolved;

                        if (!isEqual)
                        {
                            logger.Debug("Arguments differ.");
                            return false;
                        }
                    }
                }

                return true;
            });
        }

        private static void StubSessionToFailForType<T>(IDummyValueCreationSession session)
        {
            object outResult;
            A.CallTo(() => session.TryResolveDummyValue(typeof(T), out outResult))
                .Returns(false);
        }

        private static void StubSessionWithDummyValue<T>(IDummyValueCreationSession session, T dummyValue)
        {
            object outResult;
            A.CallTo(() => session.TryResolveDummyValue(typeof(T), out outResult))
                .Returns(true)
                .AssignsOutAndRefParameters(dummyValue);
        }

        private void StubProxyGeneratorToFail(string failReason)
        {
            A.CallTo(() => this.proxyGenerator.GenerateProxy(A<Type>.Ignored, A<IEnumerable<Type>>.Ignored.Argument, A<IEnumerable<object>>.Ignored.Argument))
                .Returns(new ProxyGeneratorResult(failReason));
        }

        private void StubProxyGeneratorToFail()
        {
            this.StubProxyGeneratorToFail("failed");
        }

        public class TypeWithMultipleConstructors
        {
            public TypeWithMultipleConstructors()
            {
            }

            public TypeWithMultipleConstructors(int argument1)
            {
            }

            public TypeWithMultipleConstructors(int argument1, int argument2)
            {
            }
        }

        public class TypeWithConstructorThatTakesDifferentTypes
        {
            public TypeWithConstructorThatTakesDifferentTypes(int argument1, string argument2)
            {
            }
        }

        public class TypeWithProtectedConstructor
        {
            protected TypeWithProtectedConstructor(int argument)
            {
            }
        }
    }
}