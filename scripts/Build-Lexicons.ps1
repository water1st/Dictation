#requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $JmDictPath,

    [Parameter(Mandatory)]
    [string] $TomoshiDatabasePath,

    [Parameter(Mandatory)]
    [string] $WordNetYamlDirectory,

    [Parameter(Mandatory)]
    [string] $ChineseWordNetArchivePath,

    [Parameter(Mandatory)]
    [string] $EcdictArchivePath,

    [Parameter(Mandatory)]
    [string] $TemporaryDirectory,

    [Parameter(Mandatory)]
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedHashes = @{
    JmDict = '9C1DE325ED8E3D690F4DD9F9E1E114FB800ABE5B23782C43F89D9A9E44F431CF'
    Tomoshi = 'C935FFD8E030F496C251B38819BF63005729D2DBC98BA0AF8C55DE507A455290'
    ChineseWordNet = '7D07AF60A6CED0CEDC4CA114D0B60A796D3F138DF0F3BE21F6322E53D004E91C'
    Ecdict = 'EA01F76A3B3351021CE47077E89234465CC9441C8793054495320D06C0C3F3F6'
    WordNet = 'F6FA481A5B29D9F33E2178DBEE74D0D1664EB513782CA483BE88E925942BCA77'
}

function Resolve-InputFile([string] $Path, [string] $DisplayName) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not [System.IO.File]::Exists($fullPath)) {
        throw "$DisplayName 不存在：$fullPath"
    }

    return $fullPath
}

function Resolve-InputDirectory([string] $Path, [string] $DisplayName) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not [System.IO.Directory]::Exists($fullPath)) {
        throw "$DisplayName 不存在：$fullPath"
    }

    return $fullPath
}

function Assert-SafeWriteDirectory([string] $Path, [string] $DisplayName) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $segments = $fullPath.Split(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $forbidden = @('CONFIG', 'DATA', 'LOGS', 'BACKUPS', 'CACHE')
    if ($segments | Where-Object { $forbidden -contains $_.ToUpperInvariant() }) {
        throw "$DisplayName 不能位于应用运行数据目录中：$fullPath"
    }

    [System.IO.Directory]::CreateDirectory($fullPath) | Out-Null
    return $fullPath
}

function Assert-FileHash([string] $Path, [string] $ExpectedHash, [string] $DisplayName) {
    $actualHash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if (-not [string]::Equals($actualHash, $ExpectedHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$DisplayName SHA-256 不匹配。expected=$ExpectedHash actual=$actualHash path=$Path"
    }

    Write-Output "input_verified name=$DisplayName sha256=$actualHash path=$Path"
}

function Invoke-Checked([string] $Executable, [string[]] $Arguments, [string] $FailureMessage) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage exit_code=$LASTEXITCODE"
    }
}

function Invoke-SqliteScalar([string] $DatabasePath, [string] $Sql) {
    $result = @(& $script:sqlitePath -batch -noheader $DatabasePath $Sql 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "SQLite 查询失败：$($result -join [Environment]::NewLine)"
    }

    return ($result -join [Environment]::NewLine).Trim()
}

function Assert-SqliteEquals(
    [string] $DatabasePath,
    [string] $Sql,
    [string] $Expected,
    [string] $DisplayName) {
    $actual = Invoke-SqliteScalar $DatabasePath $Sql
    if (-not [string]::Equals($actual, $Expected, [StringComparison]::Ordinal)) {
        throw "$DisplayName 校验失败。expected=$Expected actual=$actual"
    }

    Write-Output "gate_passed name=$DisplayName value=$actual"
}

function Assert-SqliteAtLeastOne([string] $DatabasePath, [string] $Sql, [string] $DisplayName) {
    $actual = Invoke-SqliteScalar $DatabasePath $Sql
    if ([int64]$actual -lt 1) {
        throw "$DisplayName 校验失败。expected>=1 actual=$actual"
    }

    Write-Output "gate_passed name=$DisplayName value=$actual"
}

function Get-CowUniqueIliCount([string] $XmlPath) {
    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Ignore
    $settings.XmlResolver = $null
    $values = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $reader = [System.Xml.XmlReader]::Create($XmlPath, $settings)
    try {
        while ($reader.Read()) {
            if ($reader.NodeType -eq [System.Xml.XmlNodeType]::Element -and $reader.LocalName -eq 'Synset') {
                $ili = $reader.GetAttribute('ili')
                if (-not [string]::IsNullOrWhiteSpace($ili)) {
                    $values.Add($ili) | Out-Null
                }
            }
        }
    }
    finally {
        $reader.Dispose()
    }

    return $values.Count
}

$jmDict = Resolve-InputFile $JmDictPath 'JMdict 输入'
$tomoshi = Resolve-InputFile $TomoshiDatabasePath 'Tomoshi 输入'
$wordNet = Resolve-InputDirectory $WordNetYamlDirectory 'Open English WordNet YAML 目录'
$cowArchive = Resolve-InputFile $ChineseWordNetArchivePath 'Chinese Open WordNet 归档'
$ecdictArchive = Resolve-InputFile $EcdictArchivePath 'ECDICT 归档'
$temporaryRoot = Assert-SafeWriteDirectory $TemporaryDirectory '临时目录'
$outputRoot = Assert-SafeWriteDirectory $OutputDirectory '输出目录'
if ([string]::Equals($temporaryRoot, $outputRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw '临时目录和输出目录必须不同。'
}

Assert-FileHash $jmDict $expectedHashes.JmDict 'JMdict'
Assert-FileHash $tomoshi $expectedHashes.Tomoshi 'Tomoshi'
Assert-FileHash $cowArchive $expectedHashes.ChineseWordNet 'ChineseOpenWordNet'
Assert-FileHash $ecdictArchive $expectedHashes.Ecdict 'ECDICT'

$dotnet = Get-Command dotnet -ErrorAction Stop
$tar = Get-Command tar -ErrorAction Stop
$sqlite = Get-Command sqlite3 -ErrorAction Stop
$script:sqlitePath = $sqlite.Source

$cowRoot = Join-Path $temporaryRoot 'ChineseOpenWordNet'
$ecdictRoot = Join-Path $temporaryRoot 'Ecdict'
foreach ($path in @($cowRoot, $ecdictRoot)) {
    if ([System.IO.Directory]::Exists($path)) {
        throw "临时子目录已存在，请提供空的临时目录：$path"
    }

    [System.IO.Directory]::CreateDirectory($path) | Out-Null
}

Invoke-Checked $tar.Source @('-xf', $cowArchive, '-C', $cowRoot) '解压 Chinese Open WordNet 失败。'
Expand-Archive -LiteralPath $ecdictArchive -DestinationPath $ecdictRoot
$cowXml = Resolve-InputFile (Join-Path $cowRoot 'omw-cmn\omw-cmn.xml') '解压后的 Chinese Open WordNet XML'
$ecdictDatabase = Resolve-InputFile (Join-Path $ecdictRoot 'stardict.db') '解压后的 ECDICT 数据库'

$cowUniqueIliCount = Get-CowUniqueIliCount $cowXml
if ($cowUniqueIliCount -ne 42300) {
    throw "Chinese Open WordNet 唯一 ILI 数量不匹配。expected=42300 actual=$cowUniqueIliCount"
}
Write-Output "gate_passed name=cow_unique_ili value=$cowUniqueIliCount"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$builderProject = Join-Path $repositoryRoot 'Dictation.LexiconBuilder\Dictation.LexiconBuilder.csproj'
$builderDll = Join-Path $repositoryRoot 'Dictation.LexiconBuilder\bin\Release\net10.0\Dictation.LexiconBuilder.dll'
Invoke-Checked $dotnet.Source @('build', $builderProject, '--configuration', 'Release', '--no-restore') '编译词库构建器失败。'

$japaneseOutput = Join-Path $outputRoot 'JapaneseLexicon.db'
$englishOutput = Join-Path $outputRoot 'EnglishLexicon.db'
foreach ($path in @($japaneseOutput, $englishOutput)) {
    if ([System.IO.File]::Exists($path)) {
        throw "输出文件已存在，请提供空的输出目录：$path"
    }
}

Invoke-Checked $dotnet.Source @(
    $builderDll,
    '--jmdict', $jmDict,
    '--tomoshi', $tomoshi,
    '--output', $japaneseOutput) '构建日语词库失败。'
Invoke-Checked $dotnet.Source @(
    $builderDll,
    '--wordnet', $wordNet,
    '--cow', $cowXml,
    '--ecdict', $ecdictDatabase,
    '--output', $englishOutput) '构建英语词库失败。'

Assert-SqliteEquals $japaneseOutput 'PRAGMA integrity_check;' 'ok' 'japanese_integrity'
Assert-SqliteEquals $japaneseOutput "SELECT Value FROM BuildMetadata WHERE Key='schema_version';" '2' 'japanese_schema_version'
Assert-SqliteEquals $japaneseOutput "SELECT upper(Value) FROM BuildMetadata WHERE Key='jmdict_sha256';" $expectedHashes.JmDict 'japanese_jmdict_hash'
Assert-SqliteEquals $japaneseOutput "SELECT upper(Value) FROM BuildMetadata WHERE Key='tomoshi_sha256';" $expectedHashes.Tomoshi 'japanese_tomoshi_hash'
foreach ($entryId in @('jmdict:1358280:1', 'jmdict:1578850:1', 'jmdict:1399790:1')) {
    Assert-SqliteAtLeastOne $japaneseOutput "SELECT COUNT(*) FROM ChineseMeanings WHERE EntryId='$entryId';" "japanese_chinese_$entryId"
}
foreach ($entryId in @('jmdict:1358280:1', 'jmdict:1578850:1')) {
    Assert-SqliteEquals $japaneseOutput "SELECT COUNT(DISTINCT FormKind) FROM Forms WHERE EntryId='$entryId' AND FormKind IN ('Dictionary','Polite','Te','Ta');" '4' "japanese_forms_$entryId"
}
Assert-SqliteEquals $japaneseOutput "SELECT COUNT(DISTINCT Surface) FROM Forms WHERE EntryId='jmdict:1399790:1' AND Surface IN ('掃除','掃除する','掃除します','掃除して','掃除した');" '5' 'japanese_forms_cleaning'

Assert-SqliteEquals $englishOutput 'PRAGMA integrity_check;' 'ok' 'english_integrity'
Assert-SqliteEquals $englishOutput "SELECT Value FROM BuildMetadata WHERE Key='schema_version';" '2' 'english_schema_version'
Assert-SqliteEquals $englishOutput "SELECT upper(Value) FROM BuildMetadata WHERE Key='source_fingerprint_sha256';" $expectedHashes.WordNet 'english_wordnet_fingerprint'
Assert-SqliteEquals $englishOutput "SELECT Value FROM BuildMetadata WHERE Key='cow_exact_ili_count';" '38867' 'english_cow_intersection'
Assert-SqliteEquals $englishOutput "SELECT COUNT(*) FROM LearningMeanings WHERE Text <> '' AND Text NOT GLOB '*[^ -~]*';" '0' 'english_ascii_only_learning_meanings'

$englishFormGates = @(
    @('go%v', "'go','goes','went','gone','going'", '5'),
    @('do%v', "'do','does','did','done','doing'", '5'),
    @('child%n', "'child','children'", '2'),
    @('good%a', "'good','better','best'", '3')
)
foreach ($gate in $englishFormGates) {
    $lexemeId = $gate[0]
    $surfaces = $gate[1]
    $expectedCount = $gate[2]
    Assert-SqliteEquals $englishOutput "SELECT COUNT(DISTINCT Surface) FROM Forms WHERE LexemeId='$lexemeId' AND Surface IN ($surfaces);" $expectedCount "english_forms_$lexemeId"
    $headword = $lexemeId.Split('%')[0]
    Assert-SqliteAtLeastOne $englishOutput "SELECT COUNT(*) FROM LearningSenses AS s INNER JOIN LearningMeanings AS m ON m.LearningSenseId=s.LearningSenseId WHERE s.Headword='$headword';" "english_chinese_$headword"
}

Write-Output 'japanese_metadata_begin'
& $script:sqlitePath -batch -noheader $japaneseOutput "SELECT Key || '=' || Value FROM BuildMetadata ORDER BY Key;"
if ($LASTEXITCODE -ne 0) { throw '读取日语词库 metadata 失败。' }
Write-Output 'japanese_metadata_end'
Write-Output 'english_metadata_begin'
& $script:sqlitePath -batch -noheader $englishOutput "SELECT Key || '=' || Value FROM BuildMetadata ORDER BY Key;"
if ($LASTEXITCODE -ne 0) { throw '读取英语词库 metadata 失败。' }
Write-Output 'english_metadata_end'

foreach ($path in @($japaneseOutput, $englishOutput)) {
    $file = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    Write-Output "output_verified file=$($file.Name) bytes=$($file.Length) sha256=$hash path=$path"
}
