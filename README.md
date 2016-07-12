# Sitecore.Support.107643

SCA 8.1 does not work with XP 8.1 Udpate-2, due to breaking changes.

## Main

This repository contains Sitecore Patch #107643, which uses a new Sitecore Kernel API for applying configuration changes.

## Deployment

To apply the patch, perform the following steps on both CM and CD servers:

1. Place the `Sitecore.Support.107643.dll` assembly into the `\bin` directory.
2. Place the `z.Sitecore.Support.107643.config` file into the `\App_Config\Include` directory.

## Content 

Sitecore Patch includes the following files:

1. `\bin\Sitecore.Support.107643.dll`
2. `\App_Config\Include\z.Sitecore.Support.107643.config`

## License

This patch is licensed under the [Sitecore Corporation A/S License](./LICENSE).

## Download

Downloads are available via [GitHub Releases](https://github.com/SitecoreSupport/Sitecore.Support.107643/releases).