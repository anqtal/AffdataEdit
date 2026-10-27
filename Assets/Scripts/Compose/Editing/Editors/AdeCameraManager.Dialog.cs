using System.Globalization;
using Arcade.Compose.Dialog;
using Arcade.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Compose
{
	public partial class AdeCameraManager
	{
		private AdeSingleDialog freeCameraDialog;
		private readonly TMP_InputField[] positionFields = new TMP_InputField[3];
		private readonly TMP_InputField[] rotationFields = new TMP_InputField[3];
		private TMP_InputField speedField;

		private void OpenFreeCameraSettings()
		{
			if (!FreeCameraEnabled)
			{
				currentCameraStatus = Properties.Length - 1;
				SwitchCameraStatus();
			}
			if (!freeCameraDialog) BuildFreeCameraSettings();
			var trans = ArcCameraManager.Instance.GameplayCamera.transform;
			for (int i = 0; i < 3; i++)
			{
				positionFields[i].SetTextWithoutNotify(trans.position[i].ToString("0.###", CultureInfo.InvariantCulture));
				rotationFields[i].SetTextWithoutNotify(trans.eulerAngles[i].ToString("0.###", CultureInfo.InvariantCulture));
			}
			speedField.SetTextWithoutNotify(speed.ToString("0.0", CultureInfo.InvariantCulture));
			freeCameraDialog.Open();
		}

		private void BuildFreeCameraSettings()
		{
			freeCameraDialog = AdeUiKit.CloneDialog(AdeBasicSingleDialogContent.Instance.Dialog, "FreeCameraSettings");
			freeCameraDialog.Title.text = "自由相机";
			freeCameraDialog.ButtonText.text = "关闭";
			AdeUiKit.SetOnClick(freeCameraDialog.CompleteButton, freeCameraDialog.Close);
			var content = AdeUiKit.Content(freeCameraDialog);
			var template = AdeUiKit.FindRow(Feature.AdeObsManager.Instance.OBSDialog, "Address");
			AdeUiKit.CreateLabelRow(content, template.GetComponentInChildren<TMP_Text>(true).transform,
				"WASD 移动，Shift 加速，中键转向，中键＋滚轮调速。\n位置、旋转修改后立即生效；再次切换视角退出自由相机。");
			TMP_InputField Field(string title, System.Action<float> apply)
			{
				var row = AdeUiKit.CloneRow(template, content, title);
				row.GetComponentInChildren<TMP_Text>(true).text = title;
				var input = row.GetComponentInChildren<TMP_InputField>(true);
				input.contentType = TMP_InputField.ContentType.DecimalNumber;
				input.onEndEdit.AddListener(text =>
				{
					if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
						&& !float.IsNaN(value) && !float.IsInfinity(value)) apply(value);
				});
				return input;
			}
			speedField = Field("速度（1～20）", value =>
			{
				speed = Mathf.Clamp(value, 1, 20);
				speedField.SetTextWithoutNotify(speed.ToString("0.0", CultureInfo.InvariantCulture));
			});
			for (int i = 0; i < 3; i++)
			{
				int axis = i;
				positionFields[i] = Field("位置 " + "XYZ"[i], value =>
				{
					var trans = ArcCameraManager.Instance.GameplayCamera.transform;
					var position = trans.position;
					position[axis] = value;
					trans.position = position;
				});
				rotationFields[i] = Field("旋转 " + "XYZ"[i], value =>
				{
					var trans = ArcCameraManager.Instance.GameplayCamera.transform;
					var rotation = trans.eulerAngles;
					rotation[axis] = value;
					trans.eulerAngles = rotation;
				});
			}
			AdeUiTheme.Refresh();
		}
	}
}
