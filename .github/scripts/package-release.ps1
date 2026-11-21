[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$VersionTag = "",

    [Parameter(Mandatory = $false)]
    [string]$OutputDir = "build/release-assets"
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$projPath = Join-Path $repoRoot "CuHotbar.csproj"
$pkgPrefix = "CuHotbar"
$pluginDir = "CuHotbar"
$expectedNamespace = "CasualtiesUnknown.Hotbar"

if (-not (Test-Path -LiteralPath $projPath)) {
    throw "未找到项目文件：'$projPath'"
}

[xml]$projXml = Get-Content -LiteralPath $projPath -Raw -Encoding UTF8
$modVersion = ($projXml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if ($null -ne $modVersion) { $modVersion = $modVersion.Trim() }
if ([string]::IsNullOrWhiteSpace($modVersion)) {
    throw "无法从 '$projPath' 读取非空 <Version>。"
}

$assemblyName = ($projXml.Project.PropertyGroup | ForEach-Object { $_.AssemblyName } | Where-Object { $_ } | Select-Object -First 1)
if ($null -ne $assemblyName) { $assemblyName = $assemblyName.Trim() }
if ([string]::IsNullOrWhiteSpace($assemblyName)) {
    throw "无法从 '$projPath' 读取非空 <AssemblyName>。"
}

if ([string]::IsNullOrWhiteSpace($VersionTag)) {
    $VersionTag = "v$modVersion"
}

if (-not $PSBoundParameters.ContainsKey("OutputDir")) {
    $OutputDir = Join-Path $repoRoot "build/release-assets"
} elseif ([string]::IsNullOrWhiteSpace($OutputDir)) {
    throw "OutputDir 不能为空白字符串。"
} elseif ([System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
} else {
    $OutputDir = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutputDir))
}

Write-Host ">>> 正在执行 dotnet restore ($projPath) ..." -ForegroundColor Cyan
& dotnet restore $projPath --nologo -v minimal | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore 失败，退出码：$LASTEXITCODE"
}

function Invoke-ConfigurationBuild {
    param([Parameter(Mandatory = $true)][string]$Configuration)

    Write-Host ">>> 正在构建 $Configuration (VersionTag=$VersionTag, Version=$modVersion, AssemblyName=$assemblyName) ..." -ForegroundColor Cyan
    & dotnet build $projPath -c $Configuration --no-incremental --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build $Configuration 失败，退出码：$LASTEXITCODE"
    }
}

function Test-CompiledAssemblyMetadata {
    param(
        [Parameter(Mandatory = $true)][string]$DllPath,
        [Parameter(Mandatory = $true)][string]$ExpectedVersion,
        [Parameter(Mandatory = $true)][bool]$ExpectDisableOptimizations
    )

    if (-not (Test-Path -LiteralPath $DllPath)) {
        throw "未找到编译产物：'$DllPath'"
    }
    if ((Get-Item -LiteralPath $DllPath).Length -le 0) {
        throw "编译产物为空文件：'$DllPath'"
    }

    Add-Type -AssemblyName System.Reflection.Metadata

    $fs = [System.IO.File]::OpenRead($DllPath)
    try {
        $peReader = [System.Reflection.PortableExecutable.PEReader]::new($fs)
        try {
            if (-not $peReader.HasMetadata) {
                throw "PE 文件不包含 CLI 元数据：'$DllPath'"
            }
            $mdReader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($peReader)

            # 1. 核验 BuildInfo.Version 常量字段严格等于 <Version>
            $foundBuildInfo = $false
            $actualVersion = $null
            foreach ($typeHandle in $mdReader.TypeDefinitions) {
                $typeDef = $mdReader.GetTypeDefinition($typeHandle)
                $typeName = $mdReader.GetString($typeDef.Name)
                $typeNs = $mdReader.GetString($typeDef.Namespace)
                if ($typeName -ceq "BuildInfo" -and $typeNs -ceq $expectedNamespace) {
                    $foundBuildInfo = $true
                    foreach ($fieldHandle in $typeDef.GetFields()) {
                        $fieldDef = $mdReader.GetFieldDefinition($fieldHandle)
                        if ($mdReader.GetString($fieldDef.Name) -ceq "Version") {
                            $constHandle = $fieldDef.GetDefaultValue()
                            if ($constHandle.IsNil) {
                                throw "BuildInfo.Version 不是编译期常量字段：'$DllPath'"
                            }
                            $constant = $mdReader.GetConstant($constHandle)
                            $constBlob = $mdReader.GetBlobReader($constant.Value)
                            $actualVersion = [string]$constBlob.ReadConstant($constant.TypeCode)
                            break
                        }
                    }
                    break
                }
            }

            if (-not $foundBuildInfo) {
                throw "未在元数据中找到类型 '${expectedNamespace}.BuildInfo'：'$DllPath'"
            }
            if ($actualVersion -cne $ExpectedVersion) {
                throw "BuildInfo.Version 不匹配 ($DllPath)：实际='$actualVersion'，期望='$ExpectedVersion'"
            }

            # 2. 核验 AssemblyDefinition 上的 DebuggableAttribute 优化标记（DisableOptimizations = 0x0100）
            $asmDef = $mdReader.GetAssemblyDefinition()
            $foundDebuggable = $false
            $hasDisableOptimizations = $false

            foreach ($caHandle in $asmDef.GetCustomAttributes()) {
                $ca = $mdReader.GetCustomAttribute($caHandle)
                $ctorHandle = $ca.Constructor
                $attrNs = ""
                $attrName = ""
                $paramCount = -1

                if ($ctorHandle.Kind -eq [System.Reflection.Metadata.HandleKind]::MemberReference) {
                    $memberRef = $mdReader.GetMemberReference([System.Reflection.Metadata.MemberReferenceHandle]$ctorHandle)
                    $sigReader = $mdReader.GetBlobReader($memberRef.Signature)
                    [void]$sigReader.ReadSignatureHeader()
                    $paramCount = $sigReader.ReadCompressedInteger()

                    $parent = $memberRef.Parent
                    if ($parent.Kind -eq [System.Reflection.Metadata.HandleKind]::TypeReference) {
                        $typeRef = $mdReader.GetTypeReference([System.Reflection.Metadata.TypeReferenceHandle]$parent)
                        $attrNs = $mdReader.GetString($typeRef.Namespace)
                        $attrName = $mdReader.GetString($typeRef.Name)
                    }
                }

                if ($attrNs -ceq "System.Diagnostics" -and $attrName -ceq "DebuggableAttribute") {
                    $foundDebuggable = $true
                    $caBlob = $mdReader.GetBlobReader($ca.Value)
                    $prolog = $caBlob.ReadUInt16()
                    if ($prolog -ne 1) {
                        throw "DebuggableAttribute Blob Prolog 异常 ($prolog)：'$DllPath'"
                    }
                    if ($paramCount -eq 1) {
                        $modes = $caBlob.ReadInt32()
                        $hasDisableOptimizations = (($modes -band 0x0100) -ne 0)
                    } elseif ($paramCount -eq 2) {
                        [void]$caBlob.ReadBoolean()
                        $hasDisableOptimizations = $caBlob.ReadBoolean()
                    } else {
                        throw "未知的 DebuggableAttribute 构造函数参数数量 ($paramCount)：'$DllPath'"
                    }
                    break
                }
            }

            if (-not $foundDebuggable) {
                throw "未在 AssemblyDefinition 上找到 DebuggableAttribute：'$DllPath'"
            }
            if ($hasDisableOptimizations -ne $ExpectDisableOptimizations) {
                throw "DebuggableAttribute.DisableOptimizations 校验失败 ($DllPath)：实际=$hasDisableOptimizations，期望=$ExpectDisableOptimizations"
            }

            Write-Host "    [PE 校验通过] $(Split-Path -Leaf $DllPath): BuildInfo.Version=$actualVersion, DisableOptimizations=$hasDisableOptimizations" -ForegroundColor DarkGreen
        } finally {
            $peReader.Dispose()
        }
    } finally {
        $fs.Dispose()
    }
}

function New-PluginZipArchive {
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][hashtable[]]$Entries,
        [Parameter(Mandatory = $true)][int]$ExpectedCount
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }

    # 显式使用正斜杠写入 ZIP 条目名，确保 Windows 与 Linux 解压器均识别标准目录结构
    $fs = [System.IO.File]::Open($ZipPath, [System.IO.FileMode]::CreateNew)
    try {
        $zip = [System.IO.Compression.ZipArchive]::new($fs, [System.IO.Compression.ZipArchiveMode]::Create, $false)
        try {
            foreach ($item in $Entries) {
                $srcPath = [string]$item.SourcePath
                $entryName = ([string]$item.EntryName).Replace('\', '/')
                if (-not (Test-Path -LiteralPath $srcPath)) {
                    throw "打包缺失源文件：'$srcPath'"
                }
                if ((Get-Item -LiteralPath $srcPath).Length -le 0) {
                    throw "打包源文件为空：'$srcPath'"
                }
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $zip,
                    $srcPath,
                    $entryName,
                    [System.IO.Compression.CompressionLevel]::Optimal
                ) | Out-Null
            }
        } finally {
            $zip.Dispose()
        }
    } finally {
        $fs.Dispose()
    }

    $verifyZip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        if ($verifyZip.Entries.Count -ne $ExpectedCount) {
            throw "ZIP 条目数异常 ($ZipPath)：实际=$($verifyZip.Entries.Count)，期望=$ExpectedCount"
        }
        foreach ($entry in $verifyZip.Entries) {
            if ($entry.FullName.Contains('\')) {
                throw "ZIP 条目包含反斜杠路径 ($ZipPath)：'$($entry.FullName)'"
            }
            if (-not $entry.FullName.StartsWith("BepInEx/plugins/$pluginDir/", [System.StringComparison]::Ordinal)) {
                throw "ZIP 条目根路径异常 ($ZipPath)：'$($entry.FullName)'"
            }
            if ($entry.Length -le 0) {
                throw "ZIP 条目解压长度为 0 ($ZipPath)：'$($entry.FullName)'"
            }
        }
        Write-Host "    [ZIP 校验通过] $(Split-Path -Leaf $ZipPath): $($verifyZip.Entries.Count) 个条目，全部正斜杠路径" -ForegroundColor DarkGreen
    } finally {
        $verifyZip.Dispose()
    }
}

Invoke-ConfigurationBuild -Configuration "Release"
Invoke-ConfigurationBuild -Configuration "Debug"

$releaseDll = Join-Path $repoRoot "bin/Release/$assemblyName.dll"
$debugDll = Join-Path $repoRoot "bin/Debug/$assemblyName.dll"
$debugPdb = Join-Path $repoRoot "bin/Debug/$assemblyName.pdb"

if (-not (Test-Path -LiteralPath $debugPdb) -or (Get-Item -LiteralPath $debugPdb).Length -le 0) {
    throw "Debug 构建未生成非空调试符号文件：'$debugPdb'"
}

Test-CompiledAssemblyMetadata -DllPath $releaseDll -ExpectedVersion $modVersion -ExpectDisableOptimizations $false
Test-CompiledAssemblyMetadata -DllPath $debugDll -ExpectedVersion $modVersion -ExpectDisableOptimizations $true

$docNames = @("README.md", "README.en.md", "changes.md", "changes.en.md")
$docEntries = foreach ($doc in $docNames) {
    $docPath = Join-Path $repoRoot $doc
    if (-not (Test-Path -LiteralPath $docPath)) {
        throw "缺少发布文档文件：'$docPath'"
    }
    @{
        SourcePath = $docPath
        EntryName  = "BepInEx/plugins/$pluginDir/$doc"
    }
}

if (Test-Path -LiteralPath $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$releaseZipName = "$pkgPrefix-$VersionTag.zip"
$debugZipName = "$pkgPrefix-$VersionTag+debug.zip"
$singleReleaseDllName = "$assemblyName.dll"
$singleDebugDllName = "$assemblyName+debug.dll"

$releaseZipPath = Join-Path $OutputDir $releaseZipName
$debugZipPath = Join-Path $OutputDir $debugZipName
$singleReleaseDllPath = Join-Path $OutputDir $singleReleaseDllName
$singleDebugDllPath = Join-Path $OutputDir $singleDebugDllName

$releaseEntries = @(
    @{
        SourcePath = $releaseDll
        EntryName  = "BepInEx/plugins/$pluginDir/$assemblyName.dll"
    }
) + $docEntries

$debugEntries = @(
    @{
        SourcePath = $debugDll
        EntryName  = "BepInEx/plugins/$pluginDir/$assemblyName.dll"
    },
    @{
        SourcePath = $debugPdb
        EntryName  = "BepInEx/plugins/$pluginDir/$assemblyName.pdb"
    }
) + $docEntries

New-PluginZipArchive -ZipPath $releaseZipPath -Entries $releaseEntries -ExpectedCount 5
New-PluginZipArchive -ZipPath $debugZipPath -Entries $debugEntries -ExpectedCount 6

Copy-Item -LiteralPath $releaseDll -Destination $singleReleaseDllPath -Force
Copy-Item -LiteralPath $debugDll -Destination $singleDebugDllPath -Force

$assetNames = @(
    $releaseZipName,
    $debugZipName,
    $singleReleaseDllName,
    $singleDebugDllName
)

$hashLines = foreach ($name in $assetNames) {
    $filePath = Join-Path $OutputDir $name
    if (-not (Test-Path -LiteralPath $filePath) -or (Get-Item -LiteralPath $filePath).Length -le 0) {
        throw "发布资产缺失或为空：'$filePath'"
    }
    $sha256 = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$sha256  $name"
}

$sumsPath = Join-Path $OutputDir "SHA256SUMS.txt"
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($sumsPath, (($hashLines -join "`n") + "`n"), $utf8NoBom)

Write-Host ">>> 打包完成，5 项标准发布资产已输出至: $OutputDir" -ForegroundColor Green
$hashLines | ForEach-Object { Write-Host "    $_" }
