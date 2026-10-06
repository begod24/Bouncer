using UnityEngine;
using UnityEngine.Localization;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Kid", fileName = "Kid_")]
    public sealed class KidDefinition : ScriptableObject
    {
        public LocalizedString displayName;
        [Tooltip("Характер в одну строку — подпись на экране выбора")]
        public LocalizedString tagline;
        [Tooltip("Модель ребёнка (FBX со скелетом Humanoid)")]
        public GameObject model;

        [Header("Экран выбора")]
        [Tooltip("Состояние AC_KidSelect с позой ребёнка")]
        public string selectPose = "Pose_Think";
        public GameObject prop;
        [Tooltip("Кость, у которой реквизит. Ступня — реквизит лежит на земле под ней (мяч)")]
        public HumanBodyBones propBone = HumanBodyBones.LeftHand;
        [Tooltip("Смещение от кости в осях ребёнка (x — вправо, y — вверх, z — вперёд); у лежащего на земле y — высота")]
        public Vector3 propPosition;
        [Tooltip("Поворот реквизита в осях ребёнка")]
        public Vector3 propRotation;

        public bool PropOnGround => propBone is HumanBodyBones.LeftFoot or HumanBodyBones.RightFoot;
    }
}
