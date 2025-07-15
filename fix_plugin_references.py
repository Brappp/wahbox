import re

# Read the file
with open('Systems/OverlayManager.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Replace Plugin.Configuration with _plugin.Configuration
# But not Plugin.PluginInterface (which is static)
content = re.sub(r'\bPlugin\.Configuration\b', '_plugin.Configuration', content)

# Write the file back
with open('Systems/OverlayManager.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print("Fixed all Plugin.Configuration references to _plugin.Configuration") 