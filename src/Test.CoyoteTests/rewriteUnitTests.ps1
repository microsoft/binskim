param(
  [Parameter(Mandatory=$true)][string]$CoyoteVersion="",
  [Parameter(Mandatory=$true)][string]$Configuration,
  [Parameter(Mandatory=$true)][string]$TargetFramework
)

Write-Output "Rewrite Unit Tests with Coyote"
if ($ENV:OS) {
    dotnet ../packages/microsoft.coyote.tool/$CoyoteVersion/tools/$TargetFramework/coyote.dll rewrite rewrite.coyote.Windows.$Configuration.json
} else {
    dotnet ../packages/microsoft.coyote.tool/$CoyoteVersion/tools/$TargetFramework/coyote.dll rewrite rewrite.coyote.nonWindows.$Configuration.json
}