#import <Foundation/Foundation.h>

#ifdef __OBJC__

// Forward declarations
typedef void (*MessageCallback)(const char* message);
typedef void (*ErrorCallback)(const char* error);
typedef void (*ConnectCallback)();

@interface WebSocketMTLS : NSObject <NSURLSessionWebSocketDelegate>
@property (strong, nonatomic) NSURLSession *session;
@property (strong, nonatomic) NSURLSessionWebSocketTask *webSocketTask;
@property (strong, nonatomic) NSURLCredential *credential;
@property (assign, nonatomic) MessageCallback messageCallback;
@property (assign, nonatomic) ErrorCallback errorCallback;
@property (assign, nonatomic) ConnectCallback connectCallback;
@property (assign, nonatomic) BOOL isConnected;

- (instancetype)initWithURL:(NSString *)urlString 
                   certPath:(NSString *)certPath 
               certPassword:(NSString *)certPassword;
- (void)connect;
- (void)sendMessage:(NSString *)message;
- (void)close;
@end

@implementation WebSocketMTLS

- (instancetype)initWithURL:(NSString *)urlString 
                   certPath:(NSString *)certPath 
               certPassword:(NSString *)certPassword {
    self = [super init];
    if (self) {
        self.isConnected = NO;
        
        // Load client certificate
        NSData *certData = [NSData dataWithContentsOfFile:certPath];
        if (!certData) {
            NSLog(@"Failed to load certificate from: %@", certPath);
            if (self.errorCallback) {
                NSString *error = [NSString stringWithFormat:@"Failed to load certificate from: %@", certPath];
                self.errorCallback([error UTF8String]);
            }
            return nil;
        }
        
        NSDictionary *options = @{
            (__bridge id)kSecImportExportPassphrase: certPassword
        };
        
        CFArrayRef items = NULL;
        OSStatus status = SecPKCS12Import((__bridge CFDataRef)certData, 
                                         (__bridge CFDictionaryRef)options, 
                                         &items);
        
        if (status != errSecSuccess || !items) {
            NSLog(@"Failed to import certificate: %d", (int)status);
            if (self.errorCallback) {
                NSString *error = [NSString stringWithFormat:@"Failed to import certificate: %d", (int)status];
                self.errorCallback([error UTF8String]);
            }
            return nil;
        }
        
        NSDictionary *identityDict = (__bridge NSDictionary *)CFArrayGetValueAtIndex(items, 0);
        SecIdentityRef identity = (__bridge SecIdentityRef)identityDict[(__bridge id)kSecImportItemIdentity];
        
        SecCertificateRef certificate = NULL;
        SecIdentityCopyCertificate(identity, &certificate);
        
        self.credential = [NSURLCredential credentialWithIdentity:identity
                                                     certificates:@[(__bridge id)certificate]
                                                      persistence:NSURLCredentialPersistenceForSession];
        
        CFRelease(items);
        if (certificate) CFRelease(certificate);
        
        // Create session configuration
        NSURLSessionConfiguration *config = [NSURLSessionConfiguration defaultSessionConfiguration];
        config.timeoutIntervalForRequest = 30;
        config.timeoutIntervalForResource = 60;
        config.TLSMinimumSupportedProtocolVersion = tls_protocol_version_TLSv12;
        
        self.session = [NSURLSession sessionWithConfiguration:config
                                                     delegate:self
                                                delegateQueue:nil];
        
        // Create WebSocket task
        NSURL *url = [NSURL URLWithString:urlString];
        self.webSocketTask = [self.session webSocketTaskWithURL:url];
    }
    return self;
}

- (void)connect {
    [self.webSocketTask resume];
    [self receiveMessage];
}

- (void)receiveMessage {
    // Use explicit type instead of typeof to avoid compilation issues
    WebSocketMTLS * __weak weakSelf = self;
    [self.webSocketTask receiveMessageWithCompletionHandler:^(NSURLSessionWebSocketMessage *message, NSError *error) {
        WebSocketMTLS *strongSelf = weakSelf;
        if (!strongSelf) return;
        
        if (error) {
            NSLog(@"Receive error: %@", error.localizedDescription);
            if (strongSelf.errorCallback) {
                strongSelf.errorCallback([error.localizedDescription UTF8String]);
            }
            return;
        }
        
        if (message.type == NSURLSessionWebSocketMessageTypeString) {
            NSString *text = message.string;
            if (strongSelf.messageCallback) {
                strongSelf.messageCallback([text UTF8String]);
            }
        } else if (message.type == NSURLSessionWebSocketMessageTypeData) {
            NSData *data = message.data;
            NSString *text = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
            if (text && strongSelf.messageCallback) {
                strongSelf.messageCallback([text UTF8String]);
            }
        }
        
        [strongSelf receiveMessage];
    }];
}

- (void)sendMessage:(NSString *)message {
    if (!self.isConnected) {
        NSLog(@"Cannot send message: WebSocket not connected");
        if (self.errorCallback) {
            self.errorCallback("Cannot send message: WebSocket not connected");
        }
        return;
    }
    
    NSURLSessionWebSocketMessage *wsMessage = [[NSURLSessionWebSocketMessage alloc] 
                                               initWithString:message];
    WebSocketMTLS * __weak weakSelf = self;
    [self.webSocketTask sendMessage:wsMessage completionHandler:^(NSError *error) {
        WebSocketMTLS *strongSelf = weakSelf;
        if (!strongSelf) return;
        
        if (error) {
            NSLog(@"Send error: %@", error.localizedDescription);
            if (strongSelf.errorCallback) {
                strongSelf.errorCallback([error.localizedDescription UTF8String]);
            }
        }
    }];
}

- (void)close {
    self.isConnected = NO;
    [self.webSocketTask cancelWithCloseCode:NSURLSessionWebSocketCloseCodeNormalClosure 
                                     reason:nil];
    [self.session finishTasksAndInvalidate];
}

#pragma mark - NSURLSessionWebSocketDelegate

- (void)URLSession:(NSURLSession *)session 
      webSocketTask:(NSURLSessionWebSocketTask *)webSocketTask 
didOpenWithProtocol:(NSString *)protocol {
    NSLog(@"WebSocket connected with protocol: %@", protocol);
    self.isConnected = YES;
    
    if (self.connectCallback) {
        self.connectCallback();
    }
}

- (void)URLSession:(NSURLSession *)session 
      webSocketTask:(NSURLSessionWebSocketTask *)webSocketTask 
   didCloseWithCode:(NSURLSessionWebSocketCloseCode)closeCode 
             reason:(NSData *)reason {
    self.isConnected = NO;
    
    NSString *reasonString = @"Unknown";
    if (reason) {
        reasonString = [[NSString alloc] initWithData:reason encoding:NSUTF8StringEncoding];
        if (!reasonString) {
            reasonString = @"Unable to decode reason";
        }
    }
    
    NSLog(@"WebSocket closed with code: %ld, reason: %@", (long)closeCode, reasonString);
    
    if (self.errorCallback) {
        NSString *errorMsg = [NSString stringWithFormat:@"WebSocket closed with code: %ld, reason: %@", 
                             (long)closeCode, reasonString];
        self.errorCallback([errorMsg UTF8String]);
    }
}

#pragma mark - NSURLSessionDelegate

- (void)URLSession:(NSURLSession *)session 
              task:(NSURLSessionTask *)task 
didReceiveChallenge:(NSURLAuthenticationChallenge *)challenge 
 completionHandler:(void (^)(NSURLSessionAuthChallengeDisposition, NSURLCredential *))completionHandler {
    
    if ([challenge.protectionSpace.authenticationMethod isEqualToString:NSURLAuthenticationMethodClientCertificate]) {
        NSLog(@"Providing client certificate for mTLS");
        completionHandler(NSURLSessionAuthChallengeUseCredential, self.credential);
        
    } else if ([challenge.protectionSpace.authenticationMethod isEqualToString:NSURLAuthenticationMethodServerTrust]) {
        SecTrustRef serverTrust = challenge.protectionSpace.serverTrust;
        
        if (@available(iOS 12.0, *)) {
            CFErrorRef error = NULL;
            BOOL trusted = SecTrustEvaluateWithError(serverTrust, &error);
            
            if (trusted) {
                NSLog(@"Server certificate validation succeeded");
                NSURLCredential *credential = [NSURLCredential credentialForTrust:serverTrust];
                completionHandler(NSURLSessionAuthChallengeUseCredential, credential);
            } else {
                NSLog(@"Server certificate validation failed");
                completionHandler(NSURLSessionAuthChallengeCancelAuthenticationChallenge, nil);
                
                if (self.errorCallback) {
                    NSString *errorMsg = @"Server certificate validation failed";
                    if (error) {
                        NSString *errorDesc = (__bridge_transfer NSString *)CFErrorCopyDescription(error);
                        errorMsg = [NSString stringWithFormat:@"Server certificate validation failed: %@", errorDesc];
                        CFRelease(error);
                    }
                    self.errorCallback([errorMsg UTF8String]);
                }
            }
        } else {
            SecTrustResultType result;
            OSStatus status = SecTrustEvaluate(serverTrust, &result);
            
            if (status == errSecSuccess && 
                (result == kSecTrustResultUnspecified || result == kSecTrustResultProceed)) {
                NSLog(@"Server certificate validation succeeded (legacy)");
                NSURLCredential *credential = [NSURLCredential credentialForTrust:serverTrust];
                completionHandler(NSURLSessionAuthChallengeUseCredential, credential);
            } else {
                NSLog(@"Server certificate validation failed (legacy)");
                completionHandler(NSURLSessionAuthChallengeCancelAuthenticationChallenge, nil);
                if (self.errorCallback) {
                    self.errorCallback("Server certificate validation failed");
                }
            }
        }
    } else {
        completionHandler(NSURLSessionAuthChallengePerformDefaultHandling, nil);
    }
}

@end

#endif // __OBJC__

#pragma mark - C Interface for Unity

#ifdef __cplusplus
extern "C" {
#endif

static NSMutableDictionary *instances = nil;
static dispatch_queue_t instanceQueue = nil;

void* _CreateWebSocket(const char* url, const char* certPath, const char* certPassword) {
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instances = [NSMutableDictionary dictionary];
        instanceQueue = dispatch_queue_create("com.websocket.mtls.instances", DISPATCH_QUEUE_SERIAL);
    });
    
    NSString *urlStr = [NSString stringWithUTF8String:url];
    NSString *certPathStr = [NSString stringWithUTF8String:certPath];
    NSString *certPasswordStr = [NSString stringWithUTF8String:certPassword];
    
    WebSocketMTLS *ws = [[WebSocketMTLS alloc] initWithURL:urlStr
                                                  certPath:certPathStr
                                              certPassword:certPasswordStr];
    
    if (ws) {
        __block void *instance;
        dispatch_sync(instanceQueue, ^{
            NSNumber *key = @((long)ws);
            instances[key] = ws;
            instance = (__bridge_retained void*)ws;
        });
        return instance;
    }
    
    return NULL;
}

void _ConnectWebSocket(void* instance) {
    if (!instance) {
        NSLog(@"Error: Null instance in _ConnectWebSocket");
        return;
    }
    WebSocketMTLS *ws = (__bridge WebSocketMTLS *)instance;
    [ws connect];
}

void _SendMessage(void* instance, const char* message) {
    if (!instance) {
        NSLog(@"Error: Null instance in _SendMessage");
        return;
    }
    if (!message) {
        NSLog(@"Error: Null message in _SendMessage");
        return;
    }
    
    WebSocketMTLS *ws = (__bridge WebSocketMTLS *)instance;
    NSString *msg = [NSString stringWithUTF8String:message];
    [ws sendMessage:msg];
}

void _CloseWebSocket(void* instance) {
    if (!instance) {
        NSLog(@"Error: Null instance in _CloseWebSocket");
        return;
    }
    
    WebSocketMTLS *ws = (__bridge_transfer WebSocketMTLS *)instance;
    [ws close];
    
    dispatch_sync(instanceQueue, ^{
        NSNumber *key = @((long)ws);
        [instances removeObjectForKey:key];
    });
}

void _SetMessageCallback(void* instance, MessageCallback callback) {
    if (!instance) {
        NSLog(@"Error: Null instance in _SetMessageCallback");
        return;
    }
    WebSocketMTLS *ws = (__bridge WebSocketMTLS *)instance;
    ws.messageCallback = callback;
}

void _SetErrorCallback(void* instance, ErrorCallback callback) {
    if (!instance) {
        NSLog(@"Error: Null instance in _SetErrorCallback");
        return;
    }
    WebSocketMTLS *ws = (__bridge WebSocketMTLS *)instance;
    ws.errorCallback = callback;
}

void _SetConnectCallback(void* instance, ConnectCallback callback) {
    if (!instance) {
        NSLog(@"Error: Null instance in _SetConnectCallback");
        return;
    }
    WebSocketMTLS *ws = (__bridge WebSocketMTLS *)instance;
    ws.connectCallback = callback;
}

#ifdef __cplusplus
}
#endif
