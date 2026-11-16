param([Parameter(Mandatory=$true)][string]$Before,[Parameter(Mandatory=$true)][string]$After,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop';$repoRoot=Split-Path $PSScriptRoot -Parent
function Full-Path($path){if([IO.Path]::IsPathRooted($path)){return [IO.Path]::GetFullPath($path)}return [IO.Path]::GetFullPath((Join-Path $repoRoot $path))}
function Stats($values){$a=@($values|Sort-Object);if(!$a.Count){throw 'Empty timing sample'};$middle=[int][Math]::Floor($a.Count/2);$median=if($a.Count%2){$a[$middle]}else{($a[$middle-1]+$a[$middle])/2};return @{Count=$a.Count;Median=$median;P95=$a[[int][Math]::Ceiling($a.Count*.95)-1];Max=$a[-1]}}
function Numbers($rows,$column){return @($rows|ForEach-Object {[double]::Parse($_.$column,[Globalization.CultureInfo]::InvariantCulture)})}
function Number($n){return $n.ToString('0.0',[Globalization.CultureInfo]::InvariantCulture)}
$beforeRoot=Full-Path $Before;$afterRoot=Full-Path $After;$outputRoot=Full-Path $Output
foreach($directory in @($beforeRoot,$afterRoot)){if(!(Test-Path (Join-Path $directory 'passed.txt'))){throw 'Benchmark not successful'}}
$beforeMeta=Get-Content (Join-Path $beforeRoot 'benchmark-run.json') -Raw|ConvertFrom-Json;$afterMeta=Get-Content (Join-Path $afterRoot 'benchmark-run.json') -Raw|ConvertFrom-Json
if($beforeMeta.Runtime -cne $afterMeta.Runtime -or $beforeMeta.BenchmarkSourceSHA256 -cne $afterMeta.BenchmarkSourceSHA256){throw 'Runtime or benchmark source differs between samples'}
$b=@(Import-Csv (Join-Path $beforeRoot 'timings.csv'));$a=@(Import-Csv (Join-Path $afterRoot 'timings.csv'))
if(($b.scenario|Sort-Object -Unique|Out-String) -cne ($a.scenario|Sort-Object -Unique|Out-String)){throw 'Scenario mismatch'}
$comparison=@();$lines=@(('# Preview performance comparison — '+$afterMeta.Runtime),'','Same benchmark source, sequential runs on the same computer and Microsoft Print to PDF. Times include the existing 200 ms debounce for setting/page changes.','','| Scenario | Samples | Before median / p95 (ms) | After median / p95 (ms) | Median reduction |','|---|---:|---:|---:|---:|')
foreach($scenario in @('first-window','reopen','repeat','orientation','calibration','page')){
 $bs=@($b|Where-Object scenario -CEQ $scenario);$as=@($a|Where-Object scenario -CEQ $scenario);if($bs.Count -ne $as.Count){throw 'Sample count mismatch'}
 $stages=@{};foreach($column in @('wall_ms','shell_ms','capture_ms','queue_ms','discovery_ms','setup_ms','inspection_ms','render_ms','delivery_ms','apply_ms','total_ms')){$stages[$column]=@{Before=(Stats (Numbers $bs $column));After=(Stats (Numbers $as $column))}}
 $prior=$stages.wall_ms.Before;$next=$stages.wall_ms.After;$reduction=100*(1-$next.Median/$prior.Median)
 $comparison+=@{Scenario=$scenario;Samples=$bs.Count;Before=$prior;After=$next;ReductionPercent=$reduction;Stages=$stages}
 $lines+='| '+$scenario+' | '+$bs.Count+' | '+(Number $prior.Median)+' / '+(Number $prior.P95)+' | '+(Number $next.Median)+' / '+(Number $next.P95)+' | '+(Number $reduction)+'% |'
}
$heartbeat=@{};foreach($side in @('Before','After')){$directory=if($side -eq 'Before'){$beforeRoot}else{$afterRoot};$values=@(Get-Content (Join-Path $directory 'ui-gaps-ms.txt')|ForEach-Object {[double]::Parse($_,[Globalization.CultureInfo]::InvariantCulture)});$heartbeat[$side]=Stats $values}
$lines+=@('',('UI heartbeat median / p95 / maximum (ms): before '+(Number $heartbeat.Before.Median)+' / '+(Number $heartbeat.Before.P95)+' / '+(Number $heartbeat.Before.Max)+'; after '+(Number $heartbeat.After.Median)+' / '+(Number $heartbeat.After.P95)+' / '+(Number $heartbeat.After.Max)+'.'),'','Resource snapshots include whole-process initialization and runtime warmup. They are observations, not a repeated-preview leak bound. Cache bounds/disposal are verified by regression tests. Baseline operation-counter columns were reserved and uninstrumented; compare wall/stage times only.','',('Before application SHA256: '+$beforeMeta.ApplicationSHA256),('After application SHA256: '+$afterMeta.ApplicationSHA256),('Benchmark source SHA256: '+$afterMeta.BenchmarkSourceSHA256))
New-Item $outputRoot -ItemType Directory -Force|Out-Null
@{Runtime=$afterMeta.Runtime;BeforeMetadata=$beforeMeta;AfterMetadata=$afterMeta;Scenarios=$comparison;Heartbeat=$heartbeat}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $outputRoot 'comparison.json') -Encoding UTF8
$lines|Set-Content (Join-Path $outputRoot 'comparison.md') -Encoding UTF8
Write-Output ($lines -join "`n")
