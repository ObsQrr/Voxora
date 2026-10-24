using Xunit;

// AppPaths.SetOverride gibi statik durum kullanan testler var → paralel çalıştırma kapalı
[assembly: CollectionBehavior(DisableTestParallelization = true)]
