// Conditionally import the correct AutoFixture xunit namespace based on the CluedIn target version.
// CluedIn 5.0+ (net10.0) uses AutoFixture.Xunit3; older versions (net6.0) use AutoFixture.Xunit2.
#if CLUEDIN_V50
global using AutoFixture.Xunit3;
#else
global using AutoFixture.Xunit2;
#endif
