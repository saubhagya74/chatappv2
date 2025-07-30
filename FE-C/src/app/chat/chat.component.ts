import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Router } from '@angular/router';
import * as signalR from '@microsoft/signalr';
import { API_BASE_URL } from '../../env';
import { VideoChatComponent } from '../video-chat/video-chat.component';
// Define interfaces for type safety
interface UserInfo {
  userName: string;
  userId: string;
  profilePicUrl?: string;
}

interface ProfileInfo extends UserInfo {
  numOfFriends: number;
}

interface MessageDto {
  senderId: string;
  receiverId: string;
  content: string;
  timeStamp: string;
}

interface NotificationDto {
  RequesterId: string;
  RequesterName: string;
  RequesterPicUrl?: string;
  RequestToId: string;
  RequestToName: string;
  RequestToPicUrl?: string;
  RequestTime: string;
  RequestStatus: string;
}
@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, VideoChatComponent],
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.css'],
})
export class ChatComponent implements OnInit {
  @ViewChild('fileInput') fileInput!: ElementRef;
  private hubConnection: signalR.HubConnection | undefined;

  // Properties
  defaultProfilePic = 'https://www.w3schools.com/howto/img_avatar.png';
  profileWindowOpen = false;
  friendsWindowOpen = false;
  currentUserName: string | null = null;
  currentUserId: string | null = null;
  loadUsersReturned: UserInfo[] | null = null;
  loadUsersErrors: string | null = null;
  profileReturned: ProfileInfo | null = null;
  profileerror: string | null = null;
  receiverId: string = '';
  messageContent: string = '';
  receiverName: string = '';
  messages: MessageDto[] = [];
  searchedName = '';
  searchError: string | null = null;
  showVideoChat: boolean = false;
  notificationWindowOpen = false;
  notifications: any[] = [];
  notificationErrors: string | null = null;

  searchWindowOpen = false;
  searchActionMessage: string | null = null;

  constructor(private router: Router, private http: HttpClient) {}

  // Auth and initialization
  ngOnInit(): void {
    console.log('ngOnInit called');
    const token = localStorage.getItem('accessToken');
    if (!token) return;

    const payload = JSON.parse(atob(token.split('.')[1]));
    this.currentUserName =
      payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'];
    this.currentUserId =
      payload[
        'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'
      ];

    // Initialize SignalR
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/ChatHub`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.elapsedMilliseconds < 60000) {
            // If we've been trying for less than 60 seconds, try every 5 seconds
            return 5000;
          }
          if (retryContext.previousRetryCount < 3) {
            // If we've tried less than 3 times after the first minute, try every 10 seconds
            return 10000;
          }
          // Otherwise stop trying
          return null;
        },
      })
      .withServerTimeout(120000) // Increase server timeout to 2 minutes
      .withKeepAliveInterval(15000) // Send keep-alive every 15 seconds
      .build();

    this.hubConnection
      .start()
      .then(() => console.log('✅ SignalR Connected'))
      .catch((err) => console.error('❌ Connection error:', err));

    // Set up message handler
    this.hubConnection.on(
      'ReceiveMessage',
      (senderName: string, content: string) => {
        const timeNow = new Date().toISOString();
        const isMyMessage = senderName === this.currentUserName;
        this.messages.push({
          content,
          senderId: isMyMessage ? this.currentUserId! : this.receiverId,
          receiverId: isMyMessage ? this.receiverId : this.currentUserId!,
          timeStamp: timeNow,
        });
      }
    );

    this.loadUsers();
    this.seeProfile();
    if (this.homePage) {
      this.loadFriendPosts();
      this.loadFriends(); // Load friends if needed for other parts of the home feed
    }
  }
  sliderOpen = false;
  selectedFile: File | null = null;
  postAbout: string = '';
  isUploading = false;
  uploadMessage: string = '';
  goToHome() {
    this.router.navigate(['/home']);
  }

  private getAuthHeaders(): HttpHeaders {
    console.log('getAuthHeaders called');
    const token = localStorage.getItem('accessToken');
    return new HttpHeaders({ Authorization: `Bearer ${token}` });
  }
  toggleSlider() {
    this.sliderOpen = !this.sliderOpen;
    this.uploadMessage = '';
    this.selectedFile = null;
    this.postAbout = '';
  }
  likeReturned: any = 0;
  likeError: string | null = null;

  likepost(postid: string) {
    const headers = this.getAuthHeaders();
    this.http
      .get(`${API_BASE_URL}/chat/like/${postid}`, { headers })
      .subscribe({
        next: (data) => {
          this.likeReturned = data;
          this.likeError = null;
        },
        error: (err) => {
          this.likeReturned = null;
          this.likeError = err.error?.message || 'Something went wrong';
        },
      });
  }

  submitPost() {
    if (!this.selectedFile || !this.postAbout) return;

    this.isUploading = true;

    const formData = new FormData();
    formData.append('file', this.selectedFile);
    formData.append('postAbout', this.postAbout);
    const headers = this.getAuthHeaders();
    this.http
      .post<any>(`${API_BASE_URL}/chat/selfPost`, formData, { headers })
      .subscribe({
        next: (res) => {
          this.uploadMessage = 'Post uploaded successfully!';
          this.isUploading = false;
          this.toggleSlider();
        },
        error: (err) => {
          console.error(err);
          this.uploadMessage = 'Upload failed.';
          this.isUploading = false;
        },
      });
  }
  // --- Main View Toggle Function ---
  toggleHomePage() {
    console.log('toggleHomePage called');
    // Close any open side panels when switching to home page view
    this.profileWindowOpen = false;
    this.notificationWindowOpen = false;
    this.friendsWindowOpen = false;
    this.searchWindowOpen = false;

    // Toggle homePage state
    this.homePage = !this.homePage;

    // Load friend posts only when activating the homepage
    if (this.homePage) {
      this.loadFriendPosts();
      // Optionally reload friends too if your homepage needs that data
      this.loadFriends();
    }
  }

  homePage = false;
  friends: any[] = [];
  friendPosts: any[] = [];
  loadingFriends = false;
  loadingFriendPosts = false;
  loadFriends() {
    this.loadingFriends = true;
    const headers = this.getAuthHeaders();

    this.http
      .get<any[]>(`${API_BASE_URL}/chat/loadfriends`, { headers })
      .subscribe({
        next: (res) => {
          this.friends = res;
          this.loadingFriends = false;
        },
        error: (err) => {
          console.error('Failed to load friends:', err);
          this.loadingFriends = false;
        },
      });
  }
  loadFriendPosts() {
    this.loadingFriendPosts = true;
    const headers = this.getAuthHeaders();

    this.http
      .get<any[]>(`${API_BASE_URL}/chat/loadfriendsposts`, { headers })
      .subscribe({
        next: (res) => {
          this.friendPosts = res;
          this.loadingFriendPosts = false;
        },
        error: (err) => {
          console.error('Failed to load friend posts:', err);
          this.loadingFriendPosts = false;
        },
      });
  }

  // --- Variables for the Search Window ---
  searchReturned: {
    searchedUserName: string;
    searchedUserId: string;
    searchedNoOfFriends: number;
    searchedProfilePicUrl: string;
  } | null = null;
  messageText: string = ''; // For the message input within the search result

  // --- Toggle Functions ---

  // The toggle function for the search window
  toggleSearchWindow() {
    console.log('toggleSearchWindow called');
    // Close other windows when opening search
    this.friendsWindowOpen = false;
    this.notificationWindowOpen = false;
    this.profileWindowOpen = false;

    // Toggle the search window itself
    this.searchWindowOpen = !this.searchWindowOpen;

    // Clear previous search results and errors when closing
    if (!this.searchWindowOpen) {
      this.searchedName = '';
      this.searchReturned = null;
      this.searchError = null;
      this.searchActionMessage = null;
      this.messageText = '';
    }
  }

  // Profile methods
  getProfilePicUrl(userId: string | null): string {
    console.log('getProfilePicUrl called with userId:', userId);
    if (!userId || !this.loadUsersReturned) return this.defaultProfilePic;
    const user = this.loadUsersReturned.find((u) => u.userId === userId);
    if (!user?.profilePicUrl) return this.defaultProfilePic;

    // If the URL is already absolute (starts with http:// or https://), use it as is
    if (
      user.profilePicUrl.startsWith('http://') ||
      user.profilePicUrl.startsWith('https://')
    ) {
      return user.profilePicUrl;
    }

    // Otherwise, prefix it with the API base URL
    return `${API_BASE_URL}${user.profilePicUrl}`;
  }

  seeProfile() {
    console.log('seeProfile called');
    const headers = this.getAuthHeaders();
    this.http
      .get<ProfileInfo>(`${API_BASE_URL}/chat/seeprofile`, { headers })
      .subscribe({
        next: (data) => {
          this.profileReturned = data;
          this.profileerror = null;
        },
        error: (err) => {
          this.profileReturned = null;
          this.profileerror = err.error?.message || 'Failed to load profile';
        },
      });
  }
  // Your main toggle function for the profile window
  toggleProfileWindow() {
    console.log('toggleProfileWindow called');
    // Close all other panels to ensure only one is open at a time
    this.friendsWindowOpen = false;
    this.notificationWindowOpen = false;
    this.searchWindowOpen = false;

    // Toggle the state of the profile window
    this.profileWindowOpen = !this.profileWindowOpen;

    // If the profile window is now open, load the profile data
    if (this.profileWindowOpen) {
      this.seeProfile(); // Call your existing function to load profile data
    } else {
      // Optional: Clear profile data when closing the window
      this.profileReturned = null;
      this.profileerror = null;
    }
  }
  async onFileSelected(event: any) {
    console.log('onFileSelected called');
    const file = event.target.files[0];
    if (file) {
      this.selectedFile = file; // ✅ This enables the button for post upload

      const formData = new FormData();
      formData.append('file', file);

      try {
        const headers = this.getAuthHeaders();
        const userId = this.currentUserId;
        if (!userId) {
          console.error('No user ID found');
          return;
        }

        const response = await this.http
          .post<{ profilePicUrl: string }>(
            `${API_BASE_URL}/chat/user/${userId}/profile-photo`,
            formData,
            { headers }
          )
          .toPromise();

        if (response?.profilePicUrl) {
          if (this.profileReturned) {
            this.profileReturned.profilePicUrl = response.profilePicUrl;
          }
          this.seeProfile();
          this.loadUsers();
        }
      } catch (error) {
        console.error('Failed to upload profile picture:', error);
        alert('Failed to upload profile picture. Please try again.');
      }
    }
  }
  async onFileSelected2(event: any) {
    console.log('onFileSelected called');
    const file = event.target.files[0];
    if (file) {
      const formData = new FormData();
      formData.append('file', file);

      try {
        const headers = this.getAuthHeaders();
        const userId = this.currentUserId;
        if (!userId) {
          console.error('No user ID found');
          return;
        }

        const response = await this.http
          .post<{ profilePicUrl: string }>(
            `${API_BASE_URL}/chat/user/${userId}/profile-photo`,
            formData,
            { headers }
          )
          .toPromise();

        if (response?.profilePicUrl) {
          if (this.profileReturned) {
            this.profileReturned.profilePicUrl = response.profilePicUrl;
          }
          // Refresh profile and user list to update pictures everywhere
          this.seeProfile();
          this.loadUsers();
        }
      } catch (error) {
        console.error('Failed to upload profile picture:', error);
        alert('Failed to upload profile picture. Please try again.');
      }
    }
  }

  // User list and chat methods
  loadUsers(): void {
    console.log('loadUsers called');
    const headers = this.getAuthHeaders();
    this.http
      .get<UserInfo[]>(`${API_BASE_URL}/Chat/loadusers`, { headers })
      .subscribe({
        next: (data) => {
          this.loadUsersReturned = data;
          this.loadUsersErrors = null;
        },
        error: (err) => {
          this.loadUsersReturned = null;
          this.loadUsersErrors = err.error?.message || 'Failed to load users';
        },
      });
  }

  selectUser(user: UserInfo) {
    console.log('selectUser called with user:', user);
    this.receiverName = user.userName;
    this.receiverId = user.userId;
    this.messages = [];
    this.loadMessages();
    this.showVideoChat = false;
  }

  loadMessages() {
    console.log('loadMessages called');
    if (!this.receiverId) return;

    const headers = this.getAuthHeaders();
    this.http
      .get<MessageDto[]>(
        `${API_BASE_URL}/Chat/loadmessage/${this.receiverId}`,
        { headers }
      )
      .subscribe({
        next: (data) => {
          this.messages = data.map((m) => ({
            ...m,
            timeStamp: m.timeStamp.endsWith('Z')
              ? m.timeStamp
              : m.timeStamp + 'Z',
          }));
        },
        error: (err) => console.error('Failed to load messages', err),
      });
  }
  loadfriends: UserInfo[] | null = null;
  loadfriendsErrors: string | null = null;
  loadfriend() {
    console.log('loadfriend called');
    const headers = this.getAuthHeaders();
    this.http
      .get<UserInfo[]>(`${API_BASE_URL}/Chat/loadfriends`, { headers })
      .subscribe({
        next: (data) => {
          this.loadfriends = data;
          this.loadfriendsErrors = null;
        },
        error: (err) => {
          this.loadfriends = null;
          this.loadfriendsErrors =
            err.error?.message || 'Failed to load friends';
        },
      });
  }
  sendMessage() {
    console.log('sendMessage called');
    if (this.receiverName && this.messageContent && this.hubConnection) {
      this.hubConnection
        .invoke('SendMessage', this.receiverName, this.messageContent)
        .then(() => {
          this.messageContent = '';
        })
        .catch((err) => console.error('Send failed:', err));
    }
  }

  // Video chat methods
  startVideoCall() {
    console.log('startVideoCall called');
    if (!this.receiverId) {
      alert('Please select a user to start a video call.');
      return;
    }
    this.showVideoChat = true;
  }

  closeVideoChat() {
    console.log('closeVideoChat called');
    this.showVideoChat = false;
  }

  // Notifications
  toggleNotificationWindow() {
    console.log('toggleNotificationWindow called');
    this.profileWindowOpen = false;
    this.friendsWindowOpen = false;
    this.searchWindowOpen = false;
    this.notificationWindowOpen = !this.notificationWindowOpen;
    if (this.notificationWindowOpen) {
      this.loadNotifications();
    }
  }

  loadNotifications() {
    console.log('loadNotifications called');
    const headers = this.getAuthHeaders();
    this.http
      .get<any[]>(`${API_BASE_URL}/chat/seenotification`, { headers })
      .subscribe({
        next: (data) => {
          this.notifications = data;
          this.notificationErrors = null;
        },
        error: (err) => {
          this.notifications = [];
          this.notificationErrors =
            err.error?.message || 'Failed to load notifications';
        },
      });
  }

  // Search

  searchUser() {
    console.log('searchUser called with searchedName:', this.searchedName);
    if (!this.searchedName) return;
    const headers = this.getAuthHeaders();
    this.http
      .get<any>(`${API_BASE_URL}/chat/searchUser/${this.searchedName}`, {
        headers,
      })
      .subscribe({
        next: (data) => {
          this.searchReturned = data;
          this.searchError = null;
        },
        error: (err) => {
          this.searchReturned = null;
          this.searchError = err.error?.message || 'User not found';
        },
      });
  }

  sendFriendRequest(userId: string) {
    console.log('sendFriendRequest called with userId:', userId);
    this.searchActionMessage = null;
    const headers = this.getAuthHeaders();
    this.http
      .get<any>(`${API_BASE_URL}/chat/sendrequest/${userId}`, { headers })
      .subscribe({
        next: (data) => {
          this.searchActionMessage = 'Friend request sent!';
        },
        error: (err) => {
          this.searchActionMessage =
            err.error?.message || 'Failed to send friend request.';
        },
      });
  }
  sendMessageToUser(userName: string, messageText: string) {
    console.log('sendMessageToUser called with userName:', userName);
    this.searchActionMessage = null;
    if (!userName || !this.hubConnection) return;
    this.hubConnection
      .invoke('SendMessage', userName, `${messageText}`)
      .then(() => {
        this.searchActionMessage = 'Message sent!';
      })
      .catch((err) => {
        this.searchActionMessage = 'Failed to send message.';
      });
  }

  acceptOrDeclineRequest(friendId: string, statuschange: boolean) {
    console.log(
      'acceptOrDeclineRequest called with friendId:',
      friendId,
      'statuschange:',
      statuschange
    );
    const headers = this.getAuthHeaders();
    this.http
      .get<boolean>(
        `${API_BASE_URL}/chat/acceptordeclinerequest/${friendId}/${statuschange}`,
        { headers }
      )
      .subscribe({
        next: (data) => {
          this.loadNotifications();
          this.notificationErrors = statuschange ? 'Accepted!' : 'Declined!';
        },
        error: (err) => {
          this.notificationErrors =
            err.error?.message || 'Failed to update request.';
        },
      });
  }
  // --- Variables for the Friends Window ---
  // --- Toggle Functions ---

  // The new toggle function for the friends window
  toggleFriendsWindow() {
    console.log('toggleFriendsWindow called');
    // Close other windows when opening friends list
    this.notificationWindowOpen = false;
    this.searchWindowOpen = false;
    this.profileWindowOpen = false;

    // Toggle the friends window itself
    this.friendsWindowOpen = !this.friendsWindowOpen;

    // Load friends only when the window is opened
    if (this.friendsWindowOpen) {
      this.loadfriend(); // Call your loadfriend method
    } else {
      // Clear friends data and errors when closing
      this.loadfriends = null;
      this.loadfriendsErrors = null;
    }
  }
}
