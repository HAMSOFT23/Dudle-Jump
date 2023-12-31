﻿using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MaxyGames.UNode.Editors.Control {
	class LayerMaskFieldControl : FieldControl<LayerMask> {
		public override void Draw(Rect position, GUIContent label, object value, Type type, Action<object> onChanged, uNodeUtility.EditValueSettings settings) {
			EditorGUI.BeginChangeCheck();
			ValidateValue(ref value);
			var oldValue = (LayerMask)value;
			var newValue = EditorGUI.MaskField(
				position,
				label,
				oldValue,
				UnityEditorInternal.InternalEditorUtility.layers
			);
			if(EditorGUI.EndChangeCheck()) {
				onChanged((LayerMask)newValue);
			}
		}
	}
}