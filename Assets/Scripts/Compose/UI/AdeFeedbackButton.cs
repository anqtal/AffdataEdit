using UnityEngine;

namespace Arcade.Compose
{
    public sealed class AdeFeedbackButton : MonoBehaviour
    {
        private const string IssuesUrl = "https://github.com/anqtal/AffdataEdit/issues";
        public void OpenIssues() => Application.OpenURL(IssuesUrl);
    }
}
