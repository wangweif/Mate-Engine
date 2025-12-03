using UnityEngine;

public class ModelHidden : MonoBehaviour
{
    public GameObject model;
    public MenuActions menuActions; // 引用 MenuActions 组件

    public void ModelHiddenButton()
    {
        // 禁用模型本身
        model.SetActive(false);

        // 禁用碰撞体
        Collider collider = model.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // 【关键】禁用 MenuActions 脚本，防止继续响应快捷键
        if (menuActions != null)
        {
            menuActions.enabled = false;
        }

        // 同时关闭所有已打开的菜单
        if (menuActions != null)
        {
            menuActions.CloseAllMenus();
        }
        HiddenManager.IsModelHidden = true;
    }
}