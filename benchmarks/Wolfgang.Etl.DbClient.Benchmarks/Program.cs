using BenchmarkDotNet.Running;
// This file still configures through the deprecated property setters. Migrating it to the
// options constructors is follow-up work, tracked separately - the deprecation's purpose is
// to warn consumers, and the options constructors are covered by DbOptionsDefaultsTests.
// Several sites here assign after construction, so they cannot move to a constructor without
// restructuring the test.
#pragma warning disable CS0618


BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args);
