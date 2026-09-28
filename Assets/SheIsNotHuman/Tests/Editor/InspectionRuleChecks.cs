using System;
using UnityEditor;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp.Tests
{
    /// <summary>Editor 메뉴나 스크립트에서 호출하는 고객 코드 규칙 검사다. NUnit 자동 발견 테스트는 아니다.</summary>
    public static class InspectionRuleChecks
    {
        /// <summary>메뉴에서 규칙 검사를 실행하고 모두 통과했을 때만 확인한 사례 수를 기록한다.</summary>
        [MenuItem("Tools/She Is Not Human/Inspection MVP/Check Customer Code Rule")]
        public static void RunFromMenu() => Debug.Log("Inspection rule checks passed: " + Run());

        /// <summary>누락·불일치·대소문자·공백·무관한 필드의 15개 사례를 검사한다. 실패하면 즉시 예외를 던진다.</summary>
        public static int Run()
        {
            int checkedCount = 0;
            var npc = ScriptableObject.CreateInstance<InspectionNpcData>();
            try
            {
                Expect(null, InspectionDecision.NonPass, "missing NPC", ref checkedCount);
                npc.identity = null;
                Expect(npc, InspectionDecision.NonPass, "missing identity", ref checkedCount);
                npc.identity = new IdentityDocument();
                npc.order = null;
                Expect(npc, InspectionDecision.NonPass, "missing order", ref checkedCount);
                npc.order = new OrderDocument();
                CheckCodes(npc, null, null, InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "", "", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "C-101", null, InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "C-101", "C-101", InspectionDecision.Pass, ref checkedCount);
                CheckCodes(npc, "C-202", "C-209", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "C-303", "C-303", InspectionDecision.Pass, ref checkedCount);
                CheckCodes(npc, "C-101", "c-101", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "C-101", "C-101 ", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, " C-101", "C-101", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "C-101", "C-101\n", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, " C-101 ", " C-101 ", InspectionDecision.Pass, ref checkedCount);
                // 다른 표시 필드가 비정상이어도 고객 코드가 같으면 통과한다는 판정 범위를 고정한다.
                npc.identity.displayName = "different identity name";
                npc.displayName = "different NPC name";
                npc.order.quantity = -10;
                npc.order.orderNumber = null;
                npc.order.productName = null;
                CheckCodes(npc, "C-101", "C-101", InspectionDecision.Pass, ref checkedCount);
                return checkedCount;
            }
            // 실패한 검사도 임시 ScriptableObject를 남기지 않도록 항상 해제한다.
            finally
            {
                UnityEngine.Object.DestroyImmediate(npc);
            }
        }

        private static void CheckCodes(InspectionNpcData npc, string identity, string order,
            InspectionDecision expected, ref int checkedCount)
        {
            npc.identity.customerCode = identity;
            npc.order.customerCode = order;
            Expect(npc, expected, "codes [" + identity + "] / [" + order + "]", ref checkedCount);
        }

        private static void Expect(InspectionNpcData npc, InspectionDecision expected, string scenario, ref int checkedCount)
        {
            var actual = InspectionRule.ExpectedDecision(npc);
            if (actual != expected)
                throw new InvalidOperationException("Inspection rule failed: " + scenario + "; expected " + expected + ", got " + actual);
            checkedCount++;
        }
    }
}
