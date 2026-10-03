<?xml version="1.0" encoding="utf-8"?>
<!--
  Produces the net8 app.config from App.config by removing the three sections that
  only mean something on .NET Framework. Skyline multi-targets net472 and
  net8.0-windows off ONE App.config, and that file is 46 KB of user settings that
  change often, so this strips at build time rather than forking a second copy that
  would silently drift.

  system.data  - DbProviderFactories registration for System.Data.SQLite. On .NET
                 Framework "system.data" is a built-in section registered by
                 machine.config. .NET 8 ships no machine.config and registers
                 nothing implicitly, so an undeclared section is a hard parse error
                 that aborts ClientConfigurationSystem.EnsureInit and takes the
                 WHOLE configuration system down on first access, not just that
                 section: Skyline-daily.exe died at startup with "Unrecognized
                 configuration section system.data". Nothing reads it either way -
                 every SQLite call site constructs `new SQLiteConnection(...)`
                 directly rather than going through DbProviderFactories.
  startup      - <supportedRuntime sku=".NETFramework,Version=v4.7.2"/>, inert on net8.
  runtime      - <assemblyBinding> redirects; net8 resolves via deps.json instead.
-->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="xml" indent="yes" encoding="utf-8"/>

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="/configuration/system.data"/>
  <xsl:template match="/configuration/startup"/>
  <xsl:template match="/configuration/runtime"/>
</xsl:stylesheet>
