[Code]
(* -----------------------------------------------------------------------------
  Detection of the per-machine WiX .msi a product shipped before its Inno installer
  (the Skyline admin installer, the first Osprey packaging). Both installed into the
  same %ProgramFiles%\<product> folder a per-machine Inno install defaults to, and
  neither installer knows about the other, so they would overlay each other and
  either uninstaller would gut the other's files.

  Windows Installer keys such products on their UpgradeCode, which is what
  MsiEnumRelatedProducts enumerates. A product script calls
  LegacyMsiAbortIfInstalled from an InitializeSetup handler, for a per-machine
  install, with its old UpgradeCode(s).
  ----------------------------------------------------------------------------- *)

function MsiEnumRelatedProducts(lpUpgradeCode: String; dwReserved: Cardinal;
  iProductIndex: Cardinal; lpProductBuf: String): Cardinal;
  external 'MsiEnumRelatedProductsW@msi.dll stdcall';

{ True if an MSI product with this UpgradeCode (a braced GUID) is installed. }
function LegacyMsiProductInstalled(const UpgradeCode: String): Boolean;
var
  ProductCode: String;
begin
  { A product code is 38 characters; the API wants room for the terminator. }
  SetLength(ProductCode, 39);
  Result := MsiEnumRelatedProducts(UpgradeCode, 0, 0, ProductCode) = 0;
end;

{ Returns False (abort Setup) with an explanation when one of the UpgradeCodes is
  installed; the message names the product as Programs and Features lists it. }
function LegacyMsiAbortIfInstalled(const ProductName: String; const UpgradeCodes: array of String): Boolean;
var
  I: Integer;
begin
  Result := True;
  for I := 0 to GetArrayLength(UpgradeCodes) - 1 do
  begin
    if LegacyMsiProductInstalled(UpgradeCodes[I]) then
    begin
      Log('Legacy MSI install found: UpgradeCode ' + UpgradeCodes[I]);
      SuppressibleMsgBox(
        'A previous ' + ProductName + ' installed by its .msi installer is still present. ' +
        'It uses the same folder as this installer, so uninstall it from Programs and Features first, ' +
        'then run this installer again.',
        mbError, MB_OK, IDOK);
      Result := False;
      Exit;
    end;
  end;
end;
