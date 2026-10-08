using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerClass, MaxParallelThreads = 8)]
[assembly: Xunit.TestCaseOrderer("ServerMain.Tests.Steps.CustomTestCaseOrderer", "ServerMain.BDDTests")]
