//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

#define PWIZ_SOURCE


#pragma unmanaged
#include "ClickwrapPrompter.hpp"
#include "Std.hpp"


#pragma managed
#include "cpp_cli_utilities.hpp"


// MCC: if anybody wants to rewrite this to use WinAPI instead of .NET, knock yourself out :)
#using <System.dll>
#using <System.Drawing.dll>
#using <System.Windows.Forms.dll>
using System::String;
using namespace System::Drawing;
using namespace System::Windows::Forms;
using namespace Microsoft::Win32;


namespace pwiz {
namespace util {


PWIZ_API_DECL bool ClickwrapPrompter::prompt(const string& caption, const string& text, const string& oneTimeKey)
{
    try
    {
        // HACK(?): skip the prompt for tests which use ClickwrapPrompter (other than its unit test)
        String^ currentProcessName = System::Diagnostics::Process::GetCurrentProcess()->ProcessName;
        if (!currentProcessName->Contains("ClickwrapPrompterTest") && currentProcessName->Contains("Test"))
            return true;

        String^ registrySubkey = "Software\\ProteoWizard";
        String^ registryValue = ToSystemString(oneTimeKey)->Replace("\\", "_")->Replace("/", "_");

        RegistryKey^ regKey = nullptr;
        if (!oneTimeKey.empty())
        {
            regKey = Registry::CurrentUser->CreateSubKey(registrySubkey);
	        if (regKey == nullptr)
                throw runtime_error(ToStdString("[ClickwrapPrompter::prompt] Unable to open/create registry key \"" + registrySubkey + "\""));

            if (regKey->GetValue(registryValue) != nullptr)
                return true;
        }

        Form^ form = gcnew Form();
        form->Text = ToSystemString(caption);
        form->StartPosition = FormStartPosition::CenterScreen;
        form->Size = Size(800, 600);

        Button^ disagree = gcnew Button();
        disagree->TabIndex = 1;
        disagree->Text = "Disagree";
        disagree->AutoSize = true;
        disagree->UseVisualStyleBackColor = true;
        disagree->Anchor = AnchorStyles::Right;
        disagree->DialogResult = DialogResult::No;

        Button^ agree = gcnew Button();
        agree->TabIndex = 0;
        agree->Text = "Agree";
        agree->Size = disagree->Size;
        agree->UseVisualStyleBackColor = true;
        agree->Anchor = AnchorStyles::Right;
        agree->DialogResult = DialogResult::Yes;

        TextBox^ textBox = gcnew TextBox();
        textBox->Text = ToSystemString(text);
        textBox->TabStop = false;
        textBox->ReadOnly = true;
        textBox->Multiline = true;
        textBox->WordWrap = true;
        textBox->ScrollBars = ScrollBars::Vertical;
        textBox->HideSelection = true;
        textBox->SelectionLength = 0;
        textBox->BackColor = System::Drawing::SystemColors::Window;
        textBox->Anchor = AnchorStyles::Top | AnchorStyles::Bottom | AnchorStyles::Left | AnchorStyles::Right;

        TableLayoutPanel^ table = gcnew TableLayoutPanel();
        table->TabStop = false;
        table->ColumnCount = 2;
        table->RowCount = 2;
        table->ColumnStyles->Add(gcnew ColumnStyle(SizeType::Percent, 100.0));
        table->ColumnStyles->Add(gcnew ColumnStyle());
        table->RowStyles->Add(gcnew RowStyle(SizeType::Percent, 100.0));
        table->RowStyles->Add(gcnew RowStyle());
        table->Dock = DockStyle::Fill;
        table->GrowStyle = TableLayoutPanelGrowStyle::FixedSize;
        table->Controls->Add(textBox, 0, 0);
        table->Controls->Add(agree, 0, 1);
        table->Controls->Add(disagree, 1, 1);
        table->SetColumnSpan(textBox, 2);

        form->Controls->Add(table);

        form->AcceptButton = agree;
        form->CancelButton = disagree;

        bool agreed = form->ShowDialog() == DialogResult::Yes;

        if (regKey != nullptr)
        {
            if (agreed)
		        regKey->SetValue(registryValue, true);
	        regKey->Close();
        }

        return agreed;
    }
    catch (System::Exception^ e)
    {
        throw runtime_error(ToStdString(e->Message));
    }
}


} // namespace util
} // namespace pwiz
