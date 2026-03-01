import os
import glob
import re

files = glob.glob('tests/**/*.cs', recursive=True)

for file in files:
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()

    changed = False

    # Fix SandboxManagerService constructors
    if 'new SandboxManagerService([' in content:
        content = re.sub(
            r'new SandboxManagerService\(\[(.*?)\], defaultProvider:',
            r'new SandboxManagerService([\1], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance, defaultProvider:',
            content, flags=re.DOTALL
        )
        content = re.sub(
            r'new SandboxManagerService\(\[(.*?)\], policy\)',
            r'new SandboxManagerService([\1], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance, policy)',
            content, flags=re.DOTALL
        )
        content = re.sub(
            r'new SandboxManagerService\(\[(.*?)\]\)',
            r'new SandboxManagerService([\1], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance)',
            content, flags=re.DOTALL
        )
        changed = True
        
    # Fix missing namespaces
    if 'using Microsoft.Extensions.Logging.Abstractions;' not in content and 'NullLogger' in content:
        content = "using Microsoft.Extensions.Logging.Abstractions;\n" + content
        changed = True
        
    # Fix StartAsync -> StartSandboxAsync
    if '.StartAsync(new SandboxStartRequest' in content:
        content = re.sub(
            r'\.StartAsync\(new SandboxStartRequest\((.*?)\)\)',
            r'.StartSandboxAsync(new SandboxStartRequest(Guid.NewGuid().ToString("N"), Provider: \1))',
            content
        )
        changed = True

    # Fix StopAsync -> StopSandboxAsync
    if '.StopAsync(' in content and 'manager.StopAsync' in content:
        content = content.replace('manager.StopAsync(', 'manager.StopSandboxAsync(')
        changed = True
        
    if 'manager.StartDefaultAsync()' in content:
        content = content.replace('manager.StartDefaultAsync()', 'manager.StartDefaultAsync(Guid.NewGuid().ToString("N"))')
        changed = True

    if changed:
        with open(file, 'w', encoding='utf-8') as f:
            f.write(content)

print("Done")
