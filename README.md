# CluedIn.Connector.AzureEventHub

Supports connections to post events to Azure Event Hubs

# About CluedIn
CluedIn is the Cloud-native Master Data Management Platform that brings data teams together enabling them to deliver the foundation of high-quality, trusted data that empowers everyone to make a difference. 

We're different because we use enhanced data management techniques like [Graph](https://www.cluedin.com/graph-versus-relational-databases-which-is-best) and [Zero Upfront Modelling](https://www.cluedin.com/upfront-versus-dynamic-data-modelling) to accelerate the time taken to prepare data to deliver insight by as much as 80%. Installed in as little as 20 minutes from the [Azure Marketplace](https://azuremarketplace.microsoft.com/en-gb/marketplace/apps/cluedin.azure_cluedin?tab=Overview), CluedIn is fully integrated with [Microsoft Purview](https://www.cluedin.com/product/microsoft-purview-mdm-integration?hsCtaTracking=461021ab-7a38-41a3-93dd-cfe2325dfd35%7Cb835efc0-e9b7-4385-a1b6-75cb7632527b) and the full [Microsoft Fabric](https://www.cluedin.com/microsoft-fabric) suite, making it the preferred choice for [Azure customers](https://www.cluedin.com/microsoft-intelligent-data-platform). 

To learn more about CluedIn, [contact the team](https://www.cluedin.com/discovery-call) today.

[https://www.cluedin.com](https://www.cluedin.com)

---

## Development

### Building locally against a specific CluedIn version

Copy `Directory.Build.props.user.template` to `Directory.Build.props.user` (git-ignored) and edit the version pair:

```xml
<_CluedIn>4.7.0</_CluedIn>
<CluedInMultiVersionTargetFramework>net6.0</CluedInMultiVersionTargetFramework>
```

Available combinations:

| CluedIn version | TFM |
|---|---|
| 4.6.0 | net6.0 |
| 4.7.0 | net6.0 |
| 4.8.0 | net6.0 |
| 5.0.0-alpha.* | net10.0 |

Visual Studio will pick this up automatically on next build. For the CLI equivalent: `dotnet build /p:_CluedIn=4.7.0 /p:CluedInMultiVersionTargetFramework=net6.0`
