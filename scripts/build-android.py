"""Build an APK using an explicitly supplied Android SDK and private signing key."""
import argparse, os, pathlib, shutil, subprocess, zipfile

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--sdk',required=True,type=pathlib.Path)
    parser.add_argument('--java-bin',type=pathlib.Path)
    parser.add_argument('--keystore',required=True,type=pathlib.Path)
    parser.add_argument('--alias',default='usbbridge')
    args=parser.parse_args()
    for name in ('USB_BRIDGE_STOREPASS','USB_BRIDGE_KEYPASS'):
        if not os.environ.get(name): parser.error('Set '+name+' before signing. Passwords are not stored in source.')
    root=pathlib.Path(__file__).resolve().parent.parent
    javac=args.java_bin/'javac.exe' if args.java_bin else pathlib.Path(shutil.which('javac') or '')
    if not javac.is_file(): parser.error('Provide JDK17 with --java-bin or add it to PATH.')
    java=javac.parent/'java.exe'
    sdk=args.sdk.resolve(); tools=sdk/'build-tools/35.0.0'; android=sdk/'platforms/android-35/android.jar'
    build=root/'build/android'; classes=build/'classes'; dex=build/'dex'; dist=root/'dist'
    for folder in (classes,dex,dist): folder.mkdir(parents=True,exist_ok=True)
    def run(*command): subprocess.run([str(v) for v in command],check=True)
    source=root/'src/android'
    run(javac,'-encoding','UTF-8','-source','8','-target','8','-classpath',android,'-d',classes,*source.glob('*.java'))
    jar=build/'classes.jar'
    with zipfile.ZipFile(jar,'w') as archive:
        for file in classes.rglob('*.class'): archive.write(file,file.relative_to(classes).as_posix())
    run(java,'-cp',tools/'lib/d8.jar','com.android.tools.r8.D8','--min-api','26','--lib',android,'--output',dex,jar)
    unsigned=build/'unsigned.apk'
    run(tools/'aapt2.exe','link','-I',android,'--manifest',source/'AndroidManifest.xml','-o',unsigned)
    with zipfile.ZipFile(unsigned,'a',compression=zipfile.ZIP_DEFLATED) as archive: archive.write(dex/'classes.dex','classes.dex')
    aligned=build/'aligned.apk'
    run(tools/'zipalign.exe','-f','-p','4',unsigned,aligned)
    output=dist/'USBDataBridge.apk'
    run(java,'-jar',tools/'lib/apksigner.jar','sign','--ks',args.keystore.resolve(),'--ks-key-alias',args.alias,'--ks-pass','env:USB_BRIDGE_STOREPASS','--key-pass','env:USB_BRIDGE_KEYPASS','--out',output,aligned)
    run(java,'-jar',tools/'lib/apksigner.jar','verify','--verbose',output)
    print('APK build complete:',output)

if __name__=='__main__': main()
