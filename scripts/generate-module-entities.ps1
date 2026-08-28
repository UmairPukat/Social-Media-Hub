$root = "C:\Users\win11\Desktop\Project\Social Media Project\backend\SocialMedia.Domain\Modules"
$modules = @(
  @{ Prefix = "Integration"; Folder = "Integrations"; Ns = "SocialMedia.Domain.Modules.Integrations.Entities" },
  @{ Prefix = "AppConnection"; Folder = "AppConnections"; Ns = "SocialMedia.Domain.Modules.AppConnections.Entities" },
  @{ Prefix = "DeveloperApp"; Folder = "DeveloperApps"; Ns = "SocialMedia.Domain.Modules.DeveloperApps.Entities" }
)

function Write-EntityFile($path, $content) {
  $dir = Split-Path $path -Parent
  if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
  [System.IO.File]::WriteAllText($path, $content.TrimStart() + "`n")
}

foreach ($m in $modules) {
  $p = $m.Prefix
  $ns = $m.Ns
  $folder = Join-Path (Join-Path $root $m.Folder) "Entities"

  Write-EntityFile (Join-Path $folder "${p}Platform.cs") @"
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}Platform : PlatformEntityBase
{
    public ICollection<${p}SocialAccount> SocialAccounts { get; set; } = new List<${p}SocialAccount>();
}
"@

  Write-EntityFile (Join-Path $folder "${p}SocialAccount.cs") @"
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}SocialAccount : SocialAccountEntityBase
{
    public User? User { get; set; }
    public ${p}Platform? Platform { get; set; }
    public ${p}SocialAuth? Auth { get; set; }
    public ICollection<${p}SocialProfile> Profiles { get; set; } = new List<${p}SocialProfile>();
    public ICollection<${p}SyncJob> SyncJobs { get; set; } = new List<${p}SyncJob>();
}
"@

  Write-EntityFile (Join-Path $folder "${p}SocialAuth.cs") @"
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}SocialAuth : SocialAuthEntityBase
{
    public ${p}SocialAccount? SocialAccount { get; set; }
}
"@

  Write-EntityFile (Join-Path $folder "${p}SocialProfile.cs") @"
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}SocialProfile : SocialProfileEntityBase
{
    public ${p}SocialAccount? SocialAccount { get; set; }
    public ICollection<${p}Post> Posts { get; set; } = new List<${p}Post>();
    public ICollection<${p}Conversation> Conversations { get; set; } = new List<${p}Conversation>();
}
"@

  Write-EntityFile (Join-Path $folder "${p}Post.cs") @"
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}Post : PostEntityBase
{
    public ${p}SocialProfile? SocialProfile { get; set; }
    public ${p}Platform? Platform { get; set; }
    public ICollection<${p}Media> MediaItems { get; set; } = new List<${p}Media>();
    public ICollection<${p}Comment> Comments { get; set; } = new List<${p}Comment>();
}
"@

  Write-EntityFile (Join-Path $folder "${p}Media.cs") @"
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}Media : MediaEntityBase
{
    public ${p}Post? Post { get; set; }
}
"@

  Write-EntityFile (Join-Path $folder "${p}Comment.cs") @"
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}Comment : CommentEntityBase
{
    public ${p}Post? Post { get; set; }
    public ${p}Comment? ParentComment { get; set; }
    public ICollection<${p}Comment> Replies { get; set; } = new List<${p}Comment>();
}
"@

  Write-EntityFile (Join-Path $folder "${p}Conversation.cs") @"
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}Conversation : ConversationEntityBase
{
    public ${p}SocialProfile? SocialProfile { get; set; }
    public ICollection<${p}Message> Messages { get; set; } = new List<${p}Message>();
}
"@

  Write-EntityFile (Join-Path $folder "${p}Message.cs") @"
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}Message : MessageEntityBase
{
    public ${p}Conversation? Conversation { get; set; }
    public ICollection<${p}MessageAttachment> Attachments { get; set; } = new List<${p}MessageAttachment>();
}
"@

  Write-EntityFile (Join-Path $folder "${p}MessageAttachment.cs") @"
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}MessageAttachment : MessageAttachmentEntityBase
{
    public ${p}Message? Message { get; set; }
}
"@

  Write-EntityFile (Join-Path $folder "${p}WebhookEvent.cs") @"
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}WebhookEvent : WebhookEventEntityBase
{
    public ${p}Platform? Platform { get; set; }
}
"@

  Write-EntityFile (Join-Path $folder "${p}WebhookLog.cs") @"
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}WebhookLog : WebhookLogEntityBase
{
    public ${p}Platform? Platform { get; set; }
}
"@

  Write-EntityFile (Join-Path $folder "${p}SyncJob.cs") @"
using SocialMedia.Domain.Modules.Common.Entities;

namespace $ns;

public class ${p}SyncJob : SyncJobEntityBase
{
    public ${p}SocialAccount? SocialAccount { get; set; }
}
"@
}

Write-Host "Generated module entities in $root"
