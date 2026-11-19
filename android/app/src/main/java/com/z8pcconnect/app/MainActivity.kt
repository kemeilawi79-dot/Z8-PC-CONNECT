package com.z8pcconnect.app

import android.graphics.BitmapFactory
import android.content.pm.PackageManager
import androidx.core.content.ContextCompat
import androidx.biometric.BiometricPrompt
import androidx.core.content.ContextCompat as CoreContextCompat
import androidx.camera.view.PreviewView
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.core.Preview
import androidx.compose.ui.viewinterop.AndroidView
import android.os.Bundle
import androidx.fragment.app.FragmentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.ui.platform.LocalContext
import androidx.activity.compose.setContent
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Computer
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.Security
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.Fingerprint
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.*
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.unit.*
import androidx.compose.ui.text.font.FontWeight
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import okhttp3.*
import okio.ByteString
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.util.concurrent.TimeUnit

class MainActivity : FragmentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) { super.onCreate(savedInstanceState); setContent { Z8Theme { Z8Home() } } }
}
private val http = OkHttpClient.Builder().readTimeout(0, TimeUnit.MILLISECONDS).build()

@Composable private fun Z8Theme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = darkColorScheme(primary=Color(0xFF60A5FA), secondary=Color(0xFF93C5FD), background=Color(0xFF080D18), surface=Color(0xFF111827)), content=content)
}

@Composable private fun Z8Home() {
    var address by remember { mutableStateOf("") }; var code by remember { mutableStateOf("") }
    var token by remember { mutableStateOf("") }; var status by remember { mutableStateOf("Not connected") }
    var screen by remember { mutableStateOf<BitmapImage?>(null) }; var page by remember { mutableStateOf("Home") }; var typedText by remember { mutableStateOf("") }; val context = LocalContext.current
    var ws by remember { mutableStateOf<WebSocket?>(null) }; val scope = rememberCoroutineScope()
    fun command(json: JSONObject) { ws?.send(json.toString()) }
    Scaffold(containerColor=Color(0xFF080D18), bottomBar={ NavigationBar(containerColor=Color(0xFF101827)) {
        listOf("Home","Files","Tutorial","Security","Settings").forEach { item -> NavigationBarItem(selected=page==item,onClick={page=item},icon={Icon(when(item){"Files"->Icons.Default.Folder;"Tutorial"->Icons.Default.Computer;"Security"->Icons.Default.Security;"Settings"->Icons.Default.Settings;"Unlock"->Icons.Default.Fingerprint;"Gestures"->Icons.Default.CameraAlt;else->Icons.Default.Computer},null)},label={Text(item)}) }
    } }) { padding ->
        LazyColumn(Modifier.fillMaxSize().padding(padding).background(Brush.verticalGradient(listOf(Color(0xFF101C32),Color(0xFF080D18)))).padding(18.dp), verticalArrangement=Arrangement.spacedBy(14.dp)) {
            item { Text("Z8 PC CONNECT",style=MaterialTheme.typography.headlineMedium,fontWeight=FontWeight.Bold,color=Color.White); Text("Windows remote access Â· LAN mode",color=Color(0xFF9CAFC8)) }
            item { Card(colors=CardDefaults.cardColors(containerColor=Color(0xFF17243A)),shape=RoundedCornerShape(20.dp)) { Column(Modifier.fillMaxWidth().padding(16.dp),verticalArrangement=Arrangement.spacedBy(10.dp)) {
                Text("PC CONNECTION",color=Color.White,fontWeight=FontWeight.Bold); Text(status,color=if(token.isNotBlank()) Color(0xFF7EE0A3) else Color(0xFFFFD28A))
                OutlinedTextField(address,{address=it},label={Text("PC IP address (example 192.168.1.5)")},singleLine=true,modifier=Modifier.fillMaxWidth())
                OutlinedTextField(code,{code=it},label={Text("Pairing code shown on Windows")},singleLine=true,modifier=Modifier.fillMaxWidth())
                Button(onClick={ scope.launch { status="Pairingâ€¦"; try {
                    val pair = withContext(Dispatchers.IO) { val c=(URL("http://${address.trim()}:8765/pair").openConnection() as HttpURLConnection); c.requestMethod="POST"; c.doOutput=true; c.setRequestProperty("Content-Type","application/json"); c.outputStream.use{it.write(JSONObject().put("code",code.trim()).toString().toByteArray())}; if(c.responseCode !in 200..299) error("Pairing rejected (${c.responseCode})"); JSONObject(c.inputStream.bufferedReader().readText()).getString("token") }
                    token=pair; status="Paired Â· connecting live desktopâ€¦"
                    val request=Request.Builder().url("ws://${address.trim()}:8765/remote?token=$token").build()
                    ws=http.newWebSocket(request,object:WebSocketListener(){ override fun onOpen(webSocket:WebSocket,response:Response){ status="Connected Â· live desktop" }; override fun onMessage(webSocket:WebSocket,bytes:ByteString){ val bmp=BitmapFactory.decodeByteArray(bytes.toByteArray(),0,bytes.size); if(bmp!=null) screen=BitmapImage(bmp) }; override fun onFailure(webSocket:WebSocket,t:Throwable,response:Response?){status="Connection failed: ${t.message}";token=""}; override fun onClosed(webSocket:WebSocket,code:Int,reason:String){status="Disconnected"} })
                } catch(e:Exception){status="Connection failed: ${e.message}"} } },modifier=Modifier.fillMaxWidth()) { Text("Pair and connect") }
                OutlinedButton(onClick={ws?.close(1000,"User disconnected");ws=null;token="";screen=null;status="Disconnected"},modifier=Modifier.fillMaxWidth()) { Text("Disconnect") }
            } } }
            if (page=="Home" && screen!=null) item {
                Text("LIVE WINDOWS SCREEN",color=Color.White,fontWeight=FontWeight.Bold)
                Image(
                    bitmap=screen!!.bitmap.asImageBitmap(), contentDescription="Live Windows desktop",
                    modifier=Modifier.fillMaxWidth().heightIn(max=420.dp).background(Color.Black)
                        .pointerInput(screen) {
                            detectTapGestures(
                                onTap = { o ->
                                    val b=screen!!.bitmap
                                    command(JSONObject().put("type","move").put("x",(o.x/size.width*b.width).toInt()).put("y",(o.y/size.height*b.height).toInt()))
                                    command(JSONObject().put("type","click"))
                                },
                                onLongPress = { command(JSONObject().put("type","rightClick")) }
                            )
                        }
                        .pointerInput(screen) {
                            detectDragGestures { change, _ ->
                                change.consume(); val b=screen!!.bitmap
                                command(JSONObject().put("type","move").put("x",(change.position.x/size.width*b.width).toInt()).put("y",(change.position.y/size.height*b.height).toInt()))
                            }
                        }
                )
                Row(horizontalArrangement=Arrangement.spacedBy(8.dp)){ Button(onClick={command(JSONObject().put("type","key").put("keyCode",0x1B))}){Text("ESC")}; Button(onClick={command(JSONObject().put("type","key").put("keyCode",0x0D))}){Text("ENTER")}; Button(onClick={command(JSONObject().put("type","key").put("keyCode",0x09))}){Text("TAB")} }
                Text("Tap = left click Â· long press = right click Â· drag = move pointer",color=Color(0xFF9CAFC8),style=MaterialTheme.typography.bodySmall)
            }
            if(page=="Home" && token.isNotBlank()) item {
                OutlinedTextField(value=typedText,onValueChange={typedText=it},label={Text("Type text to send to PC")},modifier=Modifier.fillMaxWidth())
                Button(onClick={command(JSONObject().put("type","text").put("text",typedText));typedText=""},modifier=Modifier.fillMaxWidth()){Text("Send keyboard text")}
            }
            if(page=="Files") item {
                val picker= rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri -> if(uri!=null && token.isNotBlank()) scope.launch { try {
                    val name=uri.lastPathSegment?.substringAfterLast('/')?.takeIf{it.isNotBlank()} ?: "upload.bin"
                    withContext(Dispatchers.IO) { val conn=(URL("http://${address.trim()}:8765/files/upload?name=${java.net.URLEncoder.encode(name,"UTF-8")}").openConnection() as HttpURLConnection); conn.requestMethod="POST"; conn.doOutput=true; conn.setRequestProperty("Authorization","Bearer $token"); context.contentResolver.openInputStream(uri)!!.use { input -> conn.outputStream.use { input.copyTo(it) } }; if(conn.responseCode !in 200..299) error("Upload failed: ${conn.responseCode}"); conn.disconnect() }
                    status="File uploaded to Windows PC"
                } catch(e:Exception){status="File upload failed: ${e.message}"} } }
                Text("FILE TRANSFER",color=Color.White,fontWeight=FontWeight.Bold)
                Text("Choose a file to send it to the Windows PC. Files are saved in your Windows user folder under Z8 PC CONNECT Received.",color=Color(0xFF9CAFC8))
                Button(onClick={if(token.isNotBlank()) picker.launch("*/*") else status="Connect to a PC first"},modifier=Modifier.fillMaxWidth()){Text("Select file and upload")}
            }
            if(page=="Tutorial") item {
                Text("CONNECT YOUR PHONE TO A PC",color=Color.White,fontWeight=FontWeight.Bold)
                Text("1. Open Z8 PC CONNECT on Windows. Keep its window open; it shows the PC IPv4 address and a one-time pairing code.",color=Color(0xFF9CAFC8))
                Text("2. Connect both devices to the same trusted Wi-Fi. For cable networking, connect USB and enable USB tethering; use the PC address for that tethered network.",color=Color(0xFF9CAFC8))
                Text("3. Enter the PC address and pairing code on Home, then tap Pair and connect. The code changes after a successful pairing.",color=Color(0xFF9CAFC8))
                Text("4. Tap the desktop image to click, long-press for right-click, and drag to move the pointer. Use the text field to type into the active PC app. Rotate the phone for portrait or landscape.",color=Color(0xFF9CAFC8))
                Text("5. Keep the PC awake and Z8 PC CONNECT running. Disconnect from Home when finished.",color=Color(0xFF9CAFC8))
                Text("Security: use a trusted private network only. This prototype does not unlock the Windows lock screen and must not be exposed to the public internet.",color=Color(0xFFFFD28A))
                Text("Windows and Linux downloads: GitHub release link will be added after the repository is published.",color=Color(0xFFFFD28A))
            }
            if(page=="Unlock") item {
                var biometricStatus by remember { mutableStateOf("Phone verification has not been run") }
                Text("PHONE BIOMETRIC VERIFICATION",color=Color.White,fontWeight=FontWeight.Bold)
                Text("Verify your identity on this Android phone before using sensitive Z8 controls. This does NOT unlock the Windows lock screen. Windows sign-in requires a separately installed and security-reviewed Windows Credential Provider.",color=Color(0xFFFFD28A))
                Text(biometricStatus,color=Color(0xFF9CAFC8))
                Button(onClick={
                    val activity = context as? FragmentActivity
                    if (activity == null) { biometricStatus="Biometric prompt unavailable" } else {
                        val executor = CoreContextCompat.getMainExecutor(activity)
                        val prompt = BiometricPrompt(activity, executor, object: BiometricPrompt.AuthenticationCallback() {
                            override fun onAuthenticationSucceeded(result: BiometricPrompt.AuthenticationResult) { biometricStatus="Phone identity verified for this app session" }
                            override fun onAuthenticationError(errorCode:Int, errString:CharSequence) { biometricStatus="Verification cancelled or unavailable: $errString" }
                            override fun onAuthenticationFailed() { biometricStatus="Fingerprint not recognized; try again" }
                        })
                        val info = BiometricPrompt.PromptInfo.Builder().setTitle("Verify for Z8 PC CONNECT").setSubtitle("Authenticate on this phone").setAllowedAuthenticators(androidx.biometric.BiometricManager.Authenticators.BIOMETRIC_WEAK or androidx.biometric.BiometricManager.Authenticators.DEVICE_CREDENTIAL).build()
                        prompt.authenticate(info)
                    }
                },modifier=Modifier.fillMaxWidth()){ Text("Verify with fingerprint or phone PIN") }
            }
            if(page=="Gestures") item {
                var cameraGranted by remember { mutableStateOf(ContextCompat.checkSelfPermission(context, android.Manifest.permission.CAMERA)==PackageManager.PERMISSION_GRANTED) }
                val cameraPermission = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { cameraGranted=it }
                Text("AIR GESTURE CAMERA",color=Color.White,fontWeight=FontWeight.Bold)
                Text("Camera access is optional. The current source includes the permission and camera preview foundation; hand-landmark recognition and pinch/drag gesture mapping are not yet implemented, so this page does not pretend to control the PC by air gestures.",color=Color(0xFFFFD28A))
                Button(onClick={cameraPermission.launch(android.Manifest.permission.CAMERA)},modifier=Modifier.fillMaxWidth()){ Text(if(cameraGranted) "Camera permission granted" else "Grant camera permission") }
                if(cameraGranted) {
                    val lifecycleOwner = LocalLifecycleOwner.current
                    AndroidView(factory={ ctx ->
                        val previewView=PreviewView(ctx)
                        val cameraProviderFuture=ProcessCameraProvider.getInstance(ctx)
                        cameraProviderFuture.addListener({
                            try {
                                val provider=cameraProviderFuture.get()
                                val preview=Preview.Builder().build().also { it.setSurfaceProvider(previewView.surfaceProvider) }
                                provider.unbindAll()
                                provider.bindToLifecycle(lifecycleOwner, androidx.camera.core.CameraSelector.DEFAULT_FRONT_CAMERA, preview)
                            } catch (_: Exception) { }
                        }, CoreContextCompat.getMainExecutor(ctx))
                        previewView
                    },modifier=Modifier.fillMaxWidth().height(300.dp))
                    Text("Camera preview only â€” no images are uploaded by this preview component.",color=Color(0xFF9CAFC8),style=MaterialTheme.typography.bodySmall)
                }
            }
            if(page=="Security") item { Text("SECURITY",color=Color.White,fontWeight=FontWeight.Bold); Text("Pairing uses a one-time code and a random session token. This build is LAN-only and uses unencrypted HTTP/WebSocket; use only a trusted private Wi-Fi network. Do not port-forward it. Phone biometric verification is local to Android and is not Windows sign-in.",color=Color(0xFFFFD28A)) }
            if(page=="Settings") item { Text("SETTINGS",color=Color.White,fontWeight=FontWeight.Bold); Text("Server: port 8765 Â· Transport: LAN WebSocket Â· Windows account: current signed-in interactive session",color=Color(0xFF9CAFC8)) }
        }
    }
}
private data class BitmapImage(val bitmap: android.graphics.Bitmap)




