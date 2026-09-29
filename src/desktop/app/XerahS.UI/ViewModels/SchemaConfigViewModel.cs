#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.ViewModels;

public sealed class SchemaConfigFieldViewModel : ObservableObject
{
    public SchemaConfigFieldViewModel(UploaderConfigField field)
    {
        Field = field;
        _value = field.DefaultValue ?? string.Empty;
    }

    public UploaderConfigField Field { get; }

    public string Key => Field.Key;
    public string Label => Field.Label;
    public string? Description => Field.Description;
    public bool IsPassword => Field.Kind == UploaderConfigFieldKind.Password;
    public bool IsBoolean => Field.Kind == UploaderConfigFieldKind.Boolean;
    public bool IsInteger => Field.Kind == UploaderConfigFieldKind.Integer;
    public bool ShowPlainText => !IsBoolean && !IsPassword;

    private string _value;
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public bool BooleanValue
    {
        get => string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase);
        set => Value = value ? "true" : "false";
    }
}

public sealed class SchemaConfigViewModel : ObservableObject, IUploaderConfigViewModel
{
    public SchemaConfigViewModel(UploaderConfigSchema schema)
    {
        Schema = schema;
        foreach (UploaderConfigField field in schema.Fields)
        {
            Fields.Add(new SchemaConfigFieldViewModel(field));
        }
    }

    public UploaderConfigSchema Schema { get; }
    public ObservableCollection<SchemaConfigFieldViewModel> Fields { get; } = new();

    public void LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        JObject obj = JObject.Parse(json);
        foreach (SchemaConfigFieldViewModel field in Fields)
        {
            JToken? token = obj[field.Key];
            if (token != null)
            {
                field.Value = token.Type == JTokenType.Boolean
                    ? (token.Value<bool>() ? "true" : "false")
                    : token.ToString();
            }
        }
    }

    public string ToJson()
    {
        JObject obj = new();
        foreach (SchemaConfigFieldViewModel field in Fields)
        {
            obj[field.Key] = field.Field.Kind switch
            {
                UploaderConfigFieldKind.Boolean => field.BooleanValue,
                UploaderConfigFieldKind.Integer => int.TryParse(field.Value, out int number) ? number : 0,
                _ => field.Value
            };
        }

        return obj.ToString(Formatting.Indented);
    }

    public bool Validate()
    {
        foreach (SchemaConfigFieldViewModel field in Fields)
        {
            if (field.Field.Required && string.IsNullOrWhiteSpace(field.Value))
            {
                return false;
            }
        }

        return true;
    }
}
